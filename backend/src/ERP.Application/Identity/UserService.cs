using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;
using ERP.Shared.Pagination;

namespace ERP.Application.Identity;

public interface IUserService
{
    Task<PagedResult<UserResponse>> ListAsync(UserFilter filter, CancellationToken ct = default);
    Task<UserResponse> GetAsync(Guid userId, CancellationToken ct = default);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken ct = default);
    Task DeactivateAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<RoleResponse>> GetRolesAsync(Guid userId, CancellationToken ct = default);
    Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);
}

public sealed class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;

    public UserService(IUserRepository users, IRoleRepository roles, ITenantContext tenant,
        IUnitOfWork uow, IAuditWriter audit, IClock clock)
    {
        _users = users;
        _roles = roles;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _clock = clock;
    }

    public async Task<PagedResult<UserResponse>> ListAsync(UserFilter filter, CancellationToken ct = default)
    {
        var result = await _users.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<UserResponse>
        {
            Items = result.Items.Select(Map).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<UserResponse> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetAsync(_tenant.OrganizationId, userId, false, ct)
            ?? throw NotFoundException.For("User", userId);
        return Map(user);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (await _users.EmailExistsAsync(orgId, request.Email, null, ct))
            throw new DuplicateEntityException($"A user with email '{request.Email}' already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DisplayName = request.DisplayName ?? $"{request.FirstName} {request.LastName}".Trim(),
            Phone = request.Phone,
            JobTitle = request.JobTitle,
            DepartmentId = request.DepartmentId,
            ExternalIdentityId = request.ExternalIdentityId,
            Status = "ACTIVE",
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _users.AddAsync(user, token);
            _audit.Add("USER", user.Id, AuditActions.Create, null, new { user.Email });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(user);
    }

    public async Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetAsync(_tenant.OrganizationId, userId, true, ct)
            ?? throw NotFoundException.For("User", userId);

        user.FirstName = request.FirstName ?? user.FirstName;
        user.LastName = request.LastName ?? user.LastName;
        user.DisplayName = request.DisplayName ?? user.DisplayName;
        user.Phone = request.Phone ?? user.Phone;
        user.JobTitle = request.JobTitle ?? user.JobTitle;
        user.DepartmentId = request.DepartmentId ?? user.DepartmentId;
        if (!string.IsNullOrEmpty(request.Status)) user.Status = request.Status;
        user.UpdatedAt = _clock.UtcNow;
        user.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add("USER", user.Id, AuditActions.Update);
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(user);
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetAsync(_tenant.OrganizationId, userId, true, ct)
            ?? throw NotFoundException.For("User", userId);
        user.Status = "INACTIVE";
        user.UpdatedAt = _clock.UtcNow;
        user.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add("USER", user.Id, AuditActions.StatusChange, null, new { Status = "INACTIVE" });
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    public async Task<IReadOnlyList<RoleResponse>> GetRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (!await _users.ExistsAsync(orgId, userId, ct))
            throw NotFoundException.For("User", userId);
        var roles = await _users.GetRolesAsync(orgId, userId, ct);
        return roles.Select(MapRole).ToList();
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (!await _users.ExistsAsync(orgId, userId, ct))
            throw NotFoundException.For("User", userId);
        if (!await _roles.ExistsAsync(orgId, roleId, ct))
            throw new ConflictException("Role does not belong to the organization.");
        if (await _users.HasRoleAsync(userId, roleId, ct))
            return;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _users.AssignRole(new UserRole { UserId = userId, RoleId = roleId, AssignedAt = _clock.UtcNow, AssignedBy = _tenant.UserId });
            _audit.Add("USER", userId, AuditActions.Assign, null, new { roleId });
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    public async Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (!await _users.ExistsAsync(orgId, userId, ct))
            throw NotFoundException.For("User", userId);

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _users.RemoveRoleAsync(userId, roleId, token);
            _audit.Add("USER", userId, AuditActions.Update, new { roleId }, null);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    private static UserResponse Map(User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        FirstName = u.FirstName,
        LastName = u.LastName,
        DisplayName = u.DisplayName,
        Phone = u.Phone,
        JobTitle = u.JobTitle,
        DepartmentId = u.DepartmentId,
        Status = u.Status,
        CreatedAt = u.CreatedAt
    };

    private static RoleResponse MapRole(Role r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Description = r.Description,
        IsSystemRole = r.IsSystemRole,
        IsActive = r.IsActive
    };
}
