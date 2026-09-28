using ERP.Application.Abstractions;
using ERP.Domain.Constants;

namespace ERP.Application.Projects;

/// <summary>
/// Moves a project's status forward one workflow phase at a time (Planning -> Inventory Check ->
/// Shipment In Transit -> Shipment Completed) as procurement/inventory/shipment events occur.
/// Only advances if the project's CURRENT status matches the expected "from" phase, so a project
/// that was manually moved to ON_HOLD/COMPLETED/CANCELLED (or already advanced further) is left alone.
/// </summary>
public interface IProjectStatusAdvancer
{
    Task AdvanceAsync(Guid organizationId, Guid? projectId, string fromCode, string toCode, CancellationToken ct = default);
}

public sealed class ProjectStatusAdvancer : IProjectStatusAdvancer
{
    private readonly IProjectRepository _projects;
    private readonly IProjectStatusRepository _projectStatuses;
    private readonly ITenantContext _tenant;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;

    public ProjectStatusAdvancer(IProjectRepository projects, IProjectStatusRepository projectStatuses,
        ITenantContext tenant, IAuditWriter audit, IClock clock)
    {
        _projects = projects;
        _projectStatuses = projectStatuses;
        _tenant = tenant;
        _audit = audit;
        _clock = clock;
    }

    public async Task AdvanceAsync(Guid organizationId, Guid? projectId, string fromCode, string toCode, CancellationToken ct = default)
    {
        if (projectId is null) return;

        var project = await _projects.GetByIdAsync(organizationId, projectId.Value, track: true, ct);
        if (project is null) return;

        var statuses = await _projectStatuses.ListAsync(organizationId, ct);
        var currentCode = statuses.FirstOrDefault(s => s.Id == project.StatusId)?.Code;
        if (!string.Equals(currentCode, fromCode, StringComparison.OrdinalIgnoreCase)) return;

        var target = statuses.FirstOrDefault(s => string.Equals(s.Code, toCode, StringComparison.OrdinalIgnoreCase));
        if (target is null || target.Id == project.StatusId) return;

        var previousStatusId = project.StatusId;
        project.StatusId = target.Id;
        project.UpdatedAt = _clock.UtcNow;
        project.UpdatedBy = _tenant.UserId;
        _audit.Add(EntityTypes.Project, project.Id, AuditActions.StatusChange,
            new { StatusId = previousStatusId }, new { StatusId = target.Id, Code = target.Code });
    }
}
