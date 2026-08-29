using ERP.Domain.Entities;
using ERP.Shared.Pagination;

namespace ERP.Application.Abstractions;

public sealed class ProjectFilter : PageRequest
{
    public Guid? StatusId { get; set; }
    public Guid? PriorityId { get; set; }
    public Guid? ManagerId { get; set; }
    public Guid? ClientId { get; set; }
    public DateOnly? StartDateFrom { get; set; }
    public DateOnly? StartDateTo { get; set; }
    public string? Search { get; set; }
}

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default);
    Task<PagedResult<Project>> ListAsync(Guid organizationId, ProjectFilter filter, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
}

public interface IClientRepository
{
    Task<bool> ExistsAsync(Guid organizationId, Guid clientId, CancellationToken ct = default);
    Task<IReadOnlyList<Client>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<Client?> GetAsync(Guid organizationId, Guid clientId, bool track, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Client client, CancellationToken ct = default);
}

public interface IOrganizationRepository
{
    Task<Organization?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Organization?> GetAsync(Guid id, CancellationToken ct = default);
}

public interface IDepartmentRepository
{
    Task<IReadOnlyList<Department>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid organizationId, Guid departmentId, CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<bool> ExistsAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<User?> GetAsync(Guid organizationId, Guid userId, bool track, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(Guid organizationId, string email, CancellationToken ct = default);
    Task<PagedResult<User>> ListAsync(Guid organizationId, UserFilter filter, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(Guid organizationId, string email, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetRolesAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task<bool> HasRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    void AssignRole(UserRole userRole);
    Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default);
}

public sealed class UserFilter : PageRequest
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public Guid? DepartmentId { get; set; }
}

public interface IRoleRepository
{
    Task<bool> ExistsAsync(Guid organizationId, Guid roleId, CancellationToken ct = default);
    Task<Role?> GetAsync(Guid organizationId, Guid roleId, bool track, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<bool> NameExistsAsync(Guid organizationId, string name, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Role role, CancellationToken ct = default);
    Task<bool> IsInUseAsync(Guid roleId, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetPermissionsAsync(Guid roleId, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    void AssignPermission(RolePermission rolePermission);
    Task RemovePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
}

public interface IPermissionRepository
{
    Task<IReadOnlyList<Permission>> ListAsync(CancellationToken ct = default);
    Task<Permission?> GetAsync(Guid permissionId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid permissionId, CancellationToken ct = default);
}

public interface IProjectMemberRepository
{
    Task<IReadOnlyList<ProjectMember>> ListAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectMember?> GetAsync(Guid projectId, Guid userId, bool track, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task AddAsync(ProjectMember member, CancellationToken ct = default);
    void Remove(ProjectMember member);
}

public interface IProjectStatusRepository
{
    Task<bool> ExistsAsync(Guid organizationId, Guid statusId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectStatus>> ListAsync(Guid organizationId, CancellationToken ct = default);
}

public interface IProjectPriorityRepository
{
    Task<bool> ExistsAsync(Guid organizationId, Guid priorityId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectPriority>> ListAsync(Guid organizationId, CancellationToken ct = default);
}
