using ERP.Application.Abstractions;
using ERP.Domain.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Shared.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly ErpDbContext _db;
    public DepartmentRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<Department>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Departments.AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && d.IsActive)
            .OrderBy(d => d.Name).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid organizationId, Guid departmentId, CancellationToken ct = default) =>
        _db.Departments.AnyAsync(d => d.OrganizationId == organizationId && d.Id == departmentId, ct);
}

public sealed class UserRepository : IUserRepository
{
    private readonly ErpDbContext _db;
    public UserRepository(ErpDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid organizationId, Guid userId, CancellationToken ct = default) =>
        _db.Users.AnyAsync(u => u.OrganizationId == organizationId && u.Id == userId, ct);

    public async Task<User?> GetAsync(Guid organizationId, Guid userId, bool track, CancellationToken ct = default)
    {
        var q = _db.Users.Where(u => u.OrganizationId == organizationId && u.Id == userId);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct);
    }

    public Task<User?> GetByEmailAsync(Guid organizationId, string email, CancellationToken ct = default) =>
        _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.OrganizationId == organizationId && u.Email == email, ct);

    public async Task<PagedResult<User>> ListAsync(Guid organizationId, UserFilter filter, CancellationToken ct = default)
    {
        var q = _db.Users.AsNoTracking().Where(u => u.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            q = q.Where(u => u.Status == filter.Status);
        if (filter.DepartmentId.HasValue)
            q = q.Where(u => u.DepartmentId == filter.DepartmentId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            q = q.Where(u => EF.Functions.Like(u.Email, $"%{term}%")
                || EF.Functions.Like(u.DisplayName!, $"%{term}%")
                || EF.Functions.Like(u.FirstName!, $"%{term}%")
                || EF.Functions.Like(u.LastName!, $"%{term}%"));
        }

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderBy(u => u.Email).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<User> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludeId, CancellationToken ct = default) =>
        _db.Users.AnyAsync(u => u.OrganizationId == organizationId && u.Email == email
            && (excludeId == null || u.Id != excludeId), ct);

    public async Task AddAsync(User user, CancellationToken ct = default) => await _db.Users.AddAsync(user, ct);

    public async Task<IReadOnlyList<Role>> GetRolesAsync(Guid organizationId, Guid userId, CancellationToken ct = default) =>
        await (from ur in _db.UserRoles.AsNoTracking()
               join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
               where ur.UserId == userId && r.OrganizationId == organizationId
               select r).ToListAsync(ct);

    public Task<bool> HasRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default) =>
        _db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);

    public void AssignRole(UserRole userRole) => _db.UserRoles.Add(userRole);

    public async Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var link = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);
        if (link is not null) _db.UserRoles.Remove(link);
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid userId, CancellationToken ct = default) =>
        await (from ur in _db.UserRoles.AsNoTracking()
               join rp in _db.RolePermissions.AsNoTracking() on ur.RoleId equals rp.RoleId
               join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
               where ur.UserId == userId && p.IsActive
               select p.Code).Distinct().ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default) =>
        await (from ur in _db.UserRoles.AsNoTracking()
               join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
               where ur.UserId == userId
               select r.Name).ToListAsync(ct);
}

public sealed class RoleRepository : IRoleRepository
{
    private readonly ErpDbContext _db;
    public RoleRepository(ErpDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid organizationId, Guid roleId, CancellationToken ct = default) =>
        _db.Roles.AnyAsync(r => r.OrganizationId == organizationId && r.Id == roleId, ct);

    public async Task<Role?> GetAsync(Guid organizationId, Guid roleId, bool track, CancellationToken ct = default)
    {
        var q = _db.Roles.Where(r => r.OrganizationId == organizationId && r.Id == roleId);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Role>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Roles.AsNoTracking().Where(r => r.OrganizationId == organizationId)
            .OrderBy(r => r.Name).ToListAsync(ct);

    public Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludeId, CancellationToken ct = default) =>
        _db.Roles.AnyAsync(r => r.OrganizationId == organizationId && r.Name == name
            && (excludeId == null || r.Id != excludeId), ct);

    public async Task AddAsync(Role role, CancellationToken ct = default) => await _db.Roles.AddAsync(role, ct);

    public Task<bool> IsInUseAsync(Guid roleId, CancellationToken ct = default) =>
        _db.UserRoles.AnyAsync(ur => ur.RoleId == roleId, ct);

    public async Task<IReadOnlyList<Permission>> GetPermissionsAsync(Guid roleId, CancellationToken ct = default) =>
        await (from rp in _db.RolePermissions.AsNoTracking()
               join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
               where rp.RoleId == roleId
               select p).OrderBy(p => p.Code).ToListAsync(ct);

    public Task<bool> HasPermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default) =>
        _db.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, ct);

    public void AssignPermission(RolePermission rolePermission) => _db.RolePermissions.Add(rolePermission);

    public async Task RemovePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
    {
        var link = await _db.RolePermissions.FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, ct);
        if (link is not null) _db.RolePermissions.Remove(link);
    }
}

public sealed class PermissionRepository : IPermissionRepository
{
    private readonly ErpDbContext _db;
    public PermissionRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<Permission>> ListAsync(CancellationToken ct = default) =>
        await _db.Permissions.AsNoTracking().OrderBy(p => p.Code).ToListAsync(ct);

    public Task<Permission?> GetAsync(Guid permissionId, CancellationToken ct = default) =>
        _db.Permissions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == permissionId, ct);

    public Task<bool> ExistsAsync(Guid permissionId, CancellationToken ct = default) =>
        _db.Permissions.AnyAsync(p => p.Id == permissionId, ct);
}

public sealed class ProjectMemberRepository : IProjectMemberRepository
{
    private readonly ErpDbContext _db;
    public ProjectMemberRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProjectMember>> ListAsync(Guid projectId, CancellationToken ct = default) =>
        await _db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == projectId).ToListAsync(ct);

    public async Task<ProjectMember?> GetAsync(Guid projectId, Guid userId, bool track, CancellationToken ct = default)
    {
        var q = _db.ProjectMembers.Where(m => m.ProjectId == projectId && m.UserId == userId);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct);
    }

    public Task<bool> ExistsAsync(Guid projectId, Guid userId, CancellationToken ct = default) =>
        _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);

    public async Task AddAsync(ProjectMember member, CancellationToken ct = default) => await _db.ProjectMembers.AddAsync(member, ct);

    public void Remove(ProjectMember member) => _db.ProjectMembers.Remove(member);
}
