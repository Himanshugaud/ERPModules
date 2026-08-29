using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;

namespace ERP.Application.Identity;

public interface IRoleService
{
    Task<IReadOnlyList<RoleResponse>> ListAsync(CancellationToken ct = default);
    Task<RoleResponse> GetAsync(Guid roleId, CancellationToken ct = default);
    Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken ct = default);
    Task<RoleResponse> UpdateAsync(Guid roleId, UpdateRoleRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid roleId, CancellationToken ct = default);
    Task<IReadOnlyList<PermissionResponse>> GetPermissionsAsync(Guid roleId, CancellationToken ct = default);
    Task AssignPermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task RemovePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
}

public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roles;
    private readonly IPermissionRepository _permissions;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;

    public RoleService(IRoleRepository roles, IPermissionRepository permissions, ITenantContext tenant,
        IUnitOfWork uow, IAuditWriter audit, IClock clock)
    {
        _roles = roles;
        _permissions = permissions;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _clock = clock;
    }

    public async Task<IReadOnlyList<RoleResponse>> ListAsync(CancellationToken ct = default)
    {
        var roles = await _roles.ListAsync(_tenant.OrganizationId, ct);
        return roles.Select(Map).ToList();
    }

    public async Task<RoleResponse> GetAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await _roles.GetAsync(_tenant.OrganizationId, roleId, false, ct)
            ?? throw NotFoundException.For("Role", roleId);
        return Map(role);
    }

    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (await _roles.NameExistsAsync(orgId, request.Name, null, ct))
            throw new DuplicateEntityException($"A role named '{request.Name}' already exists.");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = request.Name,
            Description = request.Description,
            IsSystemRole = false,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _roles.AddAsync(role, token);
            _audit.Add("ROLE", role.Id, AuditActions.Create, null, new { role.Name });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(role);
    }

    public async Task<RoleResponse> UpdateAsync(Guid roleId, UpdateRoleRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var role = await _roles.GetAsync(orgId, roleId, true, ct)
            ?? throw NotFoundException.For("Role", roleId);
        if (role.IsSystemRole)
            throw new ConflictException("System roles cannot be modified.");
        if (!string.Equals(role.Name, request.Name, StringComparison.Ordinal)
            && await _roles.NameExistsAsync(orgId, request.Name, roleId, ct))
            throw new DuplicateEntityException($"A role named '{request.Name}' already exists.");

        role.Name = request.Name;
        role.Description = request.Description;
        role.IsActive = request.IsActive;
        role.UpdatedAt = _clock.UtcNow;
        role.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add("ROLE", role.Id, AuditActions.Update);
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(role);
    }

    public async Task DeleteAsync(Guid roleId, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var role = await _roles.GetAsync(orgId, roleId, true, ct)
            ?? throw NotFoundException.For("Role", roleId);
        if (role.IsSystemRole)
            throw new ConflictException("System roles cannot be deleted.");
        if (await _roles.IsInUseAsync(roleId, ct))
            throw new ConflictException("Role is assigned to users and cannot be deleted.");

        role.IsActive = false;
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add("ROLE", role.Id, AuditActions.Delete);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    public async Task<IReadOnlyList<PermissionResponse>> GetPermissionsAsync(Guid roleId, CancellationToken ct = default)
    {
        if (!await _roles.ExistsAsync(_tenant.OrganizationId, roleId, ct))
            throw NotFoundException.For("Role", roleId);
        var perms = await _roles.GetPermissionsAsync(roleId, ct);
        return perms.Select(MapPermission).ToList();
    }

    public async Task AssignPermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var role = await _roles.GetAsync(orgId, roleId, false, ct)
            ?? throw NotFoundException.For("Role", roleId);
        if (role.IsSystemRole)
            throw new ConflictException("System role permissions cannot be modified.");
        if (!await _permissions.ExistsAsync(permissionId, ct))
            throw NotFoundException.For("Permission", permissionId);
        if (await _roles.HasPermissionAsync(roleId, permissionId, ct))
            return;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _roles.AssignPermission(new RolePermission { RoleId = roleId, PermissionId = permissionId, AssignedAt = _clock.UtcNow, AssignedBy = _tenant.UserId });
            _audit.Add("ROLE", roleId, AuditActions.Assign, null, new { permissionId });
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    public async Task RemovePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var role = await _roles.GetAsync(orgId, roleId, false, ct)
            ?? throw NotFoundException.For("Role", roleId);
        if (role.IsSystemRole)
            throw new ConflictException("System role permissions cannot be modified.");

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _roles.RemovePermissionAsync(roleId, permissionId, token);
            _audit.Add("ROLE", roleId, AuditActions.Update, new { permissionId }, null);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    private static RoleResponse Map(Role r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Description = r.Description,
        IsSystemRole = r.IsSystemRole,
        IsActive = r.IsActive
    };

    private static PermissionResponse MapPermission(Permission p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Name = p.Name,
        Module = p.Module,
        Resource = p.Resource,
        Action = p.Action
    };
}
