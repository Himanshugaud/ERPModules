using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;

namespace ERP.Application.Projects;

public interface IProjectMemberService
{
    Task<IReadOnlyList<ProjectMemberResponse>> ListAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectMemberResponse> AddAsync(Guid projectId, AddProjectMemberRequest request, CancellationToken ct = default);
    Task<ProjectMemberResponse> UpdateAsync(Guid projectId, Guid userId, UpdateProjectMemberRequest request, CancellationToken ct = default);
    Task RemoveAsync(Guid projectId, Guid userId, CancellationToken ct = default);
}

public sealed class ProjectMemberService : IProjectMemberService
{
    private readonly IProjectMemberRepository _members;
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;

    public ProjectMemberService(IProjectMemberRepository members, IProjectRepository projects, IUserRepository users,
        ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit, IClock clock)
    {
        _members = members;
        _projects = projects;
        _users = users;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ProjectMemberResponse>> ListAsync(Guid projectId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, ct);
        var members = await _members.ListAsync(projectId, ct);
        return members.Select(Map).ToList();
    }

    public async Task<ProjectMemberResponse> AddAsync(Guid projectId, AddProjectMemberRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        await EnsureProjectAsync(projectId, ct);
        if (!await _users.ExistsAsync(orgId, request.UserId, ct))
            throw new ConflictException("User does not belong to the organization.");
        if (await _members.ExistsAsync(projectId, request.UserId, ct))
            throw new DuplicateEntityException("User is already a member of this project.");

        var member = new ProjectMember
        {
            ProjectId = projectId,
            UserId = request.UserId,
            ProjectRole = request.ProjectRole,
            AllocationPercentage = request.AllocationPercentage,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = "ACTIVE",
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _members.AddAsync(member, token);
            _audit.Add(EntityTypes.Project, projectId, AuditActions.Assign, null, new { request.UserId, request.ProjectRole });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(member);
    }

    public async Task<ProjectMemberResponse> UpdateAsync(Guid projectId, Guid userId, UpdateProjectMemberRequest request, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, ct);
        var member = await _members.GetAsync(projectId, userId, true, ct)
            ?? throw NotFoundException.For("ProjectMember", userId);

        member.ProjectRole = request.ProjectRole ?? member.ProjectRole;
        member.AllocationPercentage = request.AllocationPercentage ?? member.AllocationPercentage;
        member.StartDate = request.StartDate ?? member.StartDate;
        member.EndDate = request.EndDate ?? member.EndDate;
        if (!string.IsNullOrEmpty(request.Status)) member.Status = request.Status;
        member.UpdatedAt = _clock.UtcNow;
        member.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.Project, projectId, AuditActions.Update, null, new { userId });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(member);
    }

    public async Task RemoveAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, ct);
        var member = await _members.GetAsync(projectId, userId, true, ct)
            ?? throw NotFoundException.For("ProjectMember", userId);

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _members.Remove(member);
            _audit.Add(EntityTypes.Project, projectId, AuditActions.Update, new { userId }, null);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    private async Task EnsureProjectAsync(Guid projectId, CancellationToken ct)
    {
        _ = await _projects.GetByIdAsync(_tenant.OrganizationId, projectId, false, ct)
            ?? throw NotFoundException.For("Project", projectId);
    }

    private static ProjectMemberResponse Map(ProjectMember m) => new()
    {
        ProjectId = m.ProjectId,
        UserId = m.UserId,
        ProjectRole = m.ProjectRole,
        AllocationPercentage = m.AllocationPercentage,
        StartDate = m.StartDate,
        EndDate = m.EndDate,
        Status = m.Status
    };
}
