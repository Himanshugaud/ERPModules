using ERP.Application.Abstractions;
using ERP.Application.Inventory;
using ERP.Application.Projects;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;
using FluentValidation;

namespace ERP.Application.Planning;

public sealed class PlanEntryResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string EntryType { get; set; } = default!;
    public string Status { get; set; } = default!;
    public Guid? AssignedTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class SavePlanEntryRequest
{
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? EntryType { get; set; }
    public string? Status { get; set; }
    public Guid? AssignedTo { get; set; }
}

public sealed class PublishPlanRequest
{
    public string? Notes { get; set; }
}

public sealed class ProjectPlanResponse
{
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = PlanStatuses.Draft;
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    public string? Notes { get; set; }
    public int DraftRequirementCount { get; set; }
    public int RequirementCount { get; set; }
    public int PlanningAttachmentCount { get; set; }
    public int CalendarEntryCount { get; set; }
    /// <summary>Fabrication work orders raised by this publish (only set on the publish response).</summary>
    public int FabricationOrdersRaised { get; set; }
}

public sealed class SavePlanEntryRequestValidator : AbstractValidator<SavePlanEntryRequest>
{
    public SavePlanEntryRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly)).WithMessage("Start date is required.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate.HasValue)
            .WithMessage("End date cannot be before the start date.");
        RuleFor(x => x).Must(x => !x.EndDate.HasValue || x.EndDate.Value.DayNumber - x.StartDate.DayNumber <= 366)
            .WithMessage("An entry can span at most one year.");
        RuleFor(x => x.EntryType).Must(t => PlanEntryTypes.All.Contains(t!)).When(x => !string.IsNullOrEmpty(x.EntryType))
            .WithMessage($"Entry type must be one of: {string.Join(", ", PlanEntryTypes.All)}");
        RuleFor(x => x.Status).Must(s => PlanEntryStatuses.All.Contains(s!)).When(x => !string.IsNullOrEmpty(x.Status))
            .WithMessage($"Status must be one of: {string.Join(", ", PlanEntryStatuses.All)}");
    }
}

public sealed class PublishPlanRequestValidator : AbstractValidator<PublishPlanRequest>
{
    public PublishPlanRequestValidator() => RuleFor(x => x.Notes).MaximumLength(1000);
}

public interface IProjectPlanService
{
    Task<ProjectPlanResponse> GetPlanAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectPlanResponse> PublishAsync(Guid projectId, PublishPlanRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PlanEntryResponse>> ListEntriesAsync(Guid projectId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<PlanEntryResponse> CreateEntryAsync(Guid projectId, SavePlanEntryRequest request, CancellationToken ct = default);
    Task<PlanEntryResponse> UpdateEntryAsync(Guid entryId, SavePlanEntryRequest request, CancellationToken ct = default);
    Task DeleteEntryAsync(Guid entryId, CancellationToken ct = default);
}

public sealed class ProjectPlanService : IProjectPlanService
{
    private readonly IProjectPlanRepository _plans;
    private readonly IProjectRepository _projects;
    private readonly IMaterialRequirementRepository _requirements;
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IProjectStatusAdvancer _statusAdvancer;
    private readonly IFabricationPlanner _fabrication;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;

    public ProjectPlanService(IProjectPlanRepository plans, IProjectRepository projects, IMaterialRequirementRepository requirements,
        IDocumentRepository documents, IUserRepository users, IProjectStatusAdvancer statusAdvancer, IFabricationPlanner fabrication, ITenantContext tenant,
        IUnitOfWork uow, IAuditWriter audit, IClock clock)
    {
        _plans = plans;
        _projects = projects;
        _requirements = requirements;
        _documents = documents;
        _users = users;
        _statusAdvancer = statusAdvancer;
        _fabrication = fabrication;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _clock = clock;
    }

    public async Task<ProjectPlanResponse> GetPlanAsync(Guid projectId, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        return await BuildPlanResponseAsync(projectId, await _plans.GetPlanAsync(_tenant.OrganizationId, projectId, false, ct), ct);
    }

    public async Task<ProjectPlanResponse> PublishAsync(Guid projectId, PublishPlanRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        await RequireProjectAsync(projectId, ct);

        var requirements = await _requirements.ListForProjectAsync(orgId, projectId, ct);
        var live = requirements.Where(r => r.Status != MaterialRequirementStatuses.Cancelled && r.Status != MaterialRequirementStatuses.Rejected).ToList();
        if (live.Count == 0)
            throw new ConflictException("Add a materials estimate before publishing the plan so Supply Chain has a material list to work from.");

        var plan = await _plans.GetPlanAsync(orgId, projectId, true, ct);
        var isNew = plan is null;
        plan ??= new ProjectPlan { ProjectId = projectId, OrganizationId = orgId };
        var fabricationRaised = 0;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            var submitted = requirements.Where(r => r.Status == MaterialRequirementStatuses.Draft).ToList();
            foreach (var draft in submitted)
            {
                draft.Status = MaterialRequirementStatuses.Submitted;
                draft.UpdatedAt = _clock.UtcNow;
                draft.UpdatedBy = _tenant.UserId;
            }

            // Factory-made items that stock cannot cover go on the fabrication list; the rest is for Supply Chain to buy.
            fabricationRaised = await _fabrication.RaiseAsync(submitted, token);
            plan.Status = PlanStatuses.Published;
            plan.PublishedAt = _clock.UtcNow;
            plan.PublishedBy = _tenant.UserId;
            plan.Notes = request.Notes?.Trim();
            plan.UpdatedAt = _clock.UtcNow;
            if (isNew) await _plans.AddPlanAsync(plan, token);

            _audit.Add(EntityTypes.ProjectPlan, projectId, AuditActions.StatusChange, null, new { plan.Status });
            await _statusAdvancer.AdvanceAsync(orgId, projectId, "PLANNING", "INVENTORY_CHECK", token);
            await _uow.SaveChangesAsync(token);
        }, ct);

        var response = await BuildPlanResponseAsync(projectId, plan, ct);
        response.FabricationOrdersRaised = fabricationRaised;
        return response;
    }

    public async Task<IReadOnlyList<PlanEntryResponse>> ListEntriesAsync(Guid projectId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        var entries = await _plans.ListEntriesAsync(_tenant.OrganizationId, projectId, from, to, ct);
        return entries.Select(ToResponse).ToList();
    }

    public async Task<PlanEntryResponse> CreateEntryAsync(Guid projectId, SavePlanEntryRequest request, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        await RequireAssigneeAsync(request.AssignedTo, ct);

        var entry = new PlanEntry
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenant.OrganizationId,
            ProjectId = projectId,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };
        Apply(entry, request);

        await _plans.AddEntryAsync(entry, ct);
        _audit.Add(EntityTypes.PlanEntry, entry.Id, AuditActions.Create, null, new { entry.Title, entry.StartDate, entry.EndDate });
        await _uow.SaveChangesAsync(ct);
        return ToResponse(entry);
    }

    public async Task<PlanEntryResponse> UpdateEntryAsync(Guid entryId, SavePlanEntryRequest request, CancellationToken ct = default)
    {
        var entry = await _plans.GetEntryAsync(_tenant.OrganizationId, entryId, true, ct) ?? throw NotFoundException.For("PlanEntry", entryId);
        await RequireAssigneeAsync(request.AssignedTo, ct);

        Apply(entry, request);
        entry.UpdatedAt = _clock.UtcNow;
        entry.UpdatedBy = _tenant.UserId;
        _audit.Add(EntityTypes.PlanEntry, entry.Id, AuditActions.Update, null, new { entry.Title, entry.StartDate, entry.EndDate, entry.Status });
        await _uow.SaveChangesAsync(ct);
        return ToResponse(entry);
    }

    public async Task DeleteEntryAsync(Guid entryId, CancellationToken ct = default)
    {
        var entry = await _plans.GetEntryAsync(_tenant.OrganizationId, entryId, true, ct) ?? throw NotFoundException.For("PlanEntry", entryId);
        _plans.RemoveEntry(entry);
        _audit.Add(EntityTypes.PlanEntry, entry.Id, AuditActions.Delete, new { entry.Title }, null);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task RequireProjectAsync(Guid projectId, CancellationToken ct)
    {
        if (await _projects.GetByIdAsync(_tenant.OrganizationId, projectId, track: false, ct) is null)
            throw NotFoundException.For("Project", projectId);
    }

    private async Task RequireAssigneeAsync(Guid? userId, CancellationToken ct)
    {
        if (userId.HasValue && !await _users.ExistsAsync(_tenant.OrganizationId, userId.Value, ct))
            throw new ConflictException("Assignee does not belong to the organization.");
    }

    private async Task<ProjectPlanResponse> BuildPlanResponseAsync(Guid projectId, ProjectPlan? plan, CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId;
        var requirements = await _requirements.ListForProjectAsync(orgId, projectId, ct);
        var live = requirements.Where(r => r.Status != MaterialRequirementStatuses.Cancelled && r.Status != MaterialRequirementStatuses.Rejected).ToList();
        return new ProjectPlanResponse
        {
            ProjectId = projectId,
            Status = plan?.Status ?? PlanStatuses.Draft,
            PublishedAt = plan?.PublishedAt,
            PublishedBy = plan?.PublishedBy,
            Notes = plan?.Notes,
            DraftRequirementCount = live.Count(r => r.Status == MaterialRequirementStatuses.Draft),
            RequirementCount = live.Count,
            PlanningAttachmentCount = await _documents.CountForEntityAsync(orgId, "PROJECT", projectId, DocumentCategories.Planning, ct),
            CalendarEntryCount = await _plans.CountEntriesAsync(orgId, projectId, ct)
        };
    }

    private static void Apply(PlanEntry entry, SavePlanEntryRequest r)
    {
        entry.Title = r.Title.Trim();
        entry.Description = r.Description?.Trim();
        entry.StartDate = r.StartDate;
        entry.EndDate = r.EndDate ?? r.StartDate;
        entry.EntryType = string.IsNullOrEmpty(r.EntryType) ? "ACTIVITY" : r.EntryType;
        entry.Status = string.IsNullOrEmpty(r.Status) ? "PLANNED" : r.Status;
        entry.AssignedTo = r.AssignedTo;
    }

    private static PlanEntryResponse ToResponse(PlanEntry e) => new()
    {
        Id = e.Id,
        ProjectId = e.ProjectId,
        StartDate = e.StartDate,
        EndDate = e.EndDate,
        Title = e.Title,
        Description = e.Description,
        EntryType = e.EntryType,
        Status = e.Status,
        AssignedTo = e.AssignedTo,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };
}
