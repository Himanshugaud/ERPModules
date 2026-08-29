using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Domain.Events;
using ERP.Shared.Exceptions;
using ERP.Shared.Pagination;

namespace ERP.Application.Projects;

public sealed class ProjectService : IProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IClientRepository _clients;
    private readonly IUserRepository _users;
    private readonly IProjectStatusRepository _statuses;
    private readonly IProjectPriorityRepository _priorities;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IOutboxWriter _outbox;
    private readonly IClock _clock;

    public ProjectService(
        IProjectRepository projects,
        IClientRepository clients,
        IUserRepository users,
        IProjectStatusRepository statuses,
        IProjectPriorityRepository priorities,
        ITenantContext tenant,
        IUnitOfWork uow,
        IAuditWriter audit,
        IOutboxWriter outbox,
        IClock clock)
    {
        _projects = projects;
        _clients = clients;
        _users = users;
        _statuses = statuses;
        _priorities = priorities;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;

        if (await _projects.CodeExistsAsync(orgId, request.Code, null, ct))
            throw new DuplicateEntityException($"A project with code '{request.Code}' already exists.");

        await ValidateReferencesAsync(request.ClientId, request.ManagerId, request.StatusId, request.PriorityId, ct);

        var now = _clock.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            ClientId = request.ClientId,
            ManagerId = request.ManagerId,
            StatusId = request.StatusId,
            PriorityId = request.PriorityId,
            StartDate = request.StartDate,
            PlannedEndDate = request.PlannedEndDate,
            Budget = request.Budget,
            CurrencyCode = request.CurrencyCode,
            CompletionPercentage = 0,
            CreatedAt = now,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _projects.AddAsync(project, token);
            _audit.Add(EntityTypes.Project, project.Id, AuditActions.Create, null, new { project.Code, project.Name });
            _outbox.Enqueue(new ProjectCreated(project.Id, project.Code, project.Name) { OrganizationId = orgId });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(project);
    }

    public async Task<PagedResult<ProjectResponse>> ListAsync(ProjectFilter filter, CancellationToken ct = default)
    {
        var result = await _projects.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<ProjectResponse>
        {
            Items = result.Items.Select(Map).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<ProjectResponse> GetAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _projects.GetByIdAsync(_tenant.OrganizationId, projectId, false, ct)
            ?? throw NotFoundException.For("Project", projectId);
        return Map(project);
    }

    public async Task<ProjectResponse> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var project = await _projects.GetByIdAsync(orgId, projectId, true, ct)
            ?? throw NotFoundException.For("Project", projectId);

        if (project.IsArchived)
            throw new ConflictException("Archived projects cannot be modified.");

        await ValidateReferencesAsync(request.ClientId, request.ManagerId, request.StatusId, request.PriorityId, ct);

        if (!string.IsNullOrEmpty(request.RowVersion))
            project.RowVersion = Convert.FromBase64String(request.RowVersion);

        project.Name = request.Name;
        project.Description = request.Description;
        project.ClientId = request.ClientId;
        project.ManagerId = request.ManagerId;
        project.StatusId = request.StatusId;
        project.PriorityId = request.PriorityId;
        project.StartDate = request.StartDate;
        project.PlannedEndDate = request.PlannedEndDate;
        project.ActualEndDate = request.ActualEndDate;
        if (request.CompletionPercentage.HasValue) project.CompletionPercentage = request.CompletionPercentage.Value;
        project.Budget = request.Budget;
        project.CurrencyCode = request.CurrencyCode;
        project.UpdatedAt = _clock.UtcNow;
        project.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.Project, project.Id, AuditActions.Update, null, new { project.Name });
            _outbox.Enqueue(new ProjectUpdated(project.Id) { OrganizationId = orgId });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(project);
    }

    public async Task DeleteAsync(Guid projectId, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var project = await _projects.GetByIdAsync(orgId, projectId, true, ct)
            ?? throw NotFoundException.For("Project", projectId);

        project.IsDeleted = true;
        project.DeletedAt = _clock.UtcNow;
        project.DeletedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.Project, project.Id, AuditActions.Delete);
            _outbox.Enqueue(new ProjectArchived(project.Id) { OrganizationId = orgId });
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    private async Task ValidateReferencesAsync(Guid? clientId, Guid? managerId, Guid? statusId, Guid? priorityId, CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId;

        if (clientId.HasValue && !await _clients.ExistsAsync(orgId, clientId.Value, ct))
            throw new ConflictException("Client does not belong to the organization.");
        if (managerId.HasValue && !await _users.ExistsAsync(orgId, managerId.Value, ct))
            throw new ConflictException("Manager does not belong to the organization.");
        if (statusId.HasValue && !await _statuses.ExistsAsync(orgId, statusId.Value, ct))
            throw new ConflictException("Status does not belong to the organization.");
        if (priorityId.HasValue && !await _priorities.ExistsAsync(orgId, priorityId.Value, ct))
            throw new ConflictException("Priority does not belong to the organization.");
    }

    private static ProjectResponse Map(Project p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Name = p.Name,
        Description = p.Description,
        ClientId = p.ClientId,
        ManagerId = p.ManagerId,
        StatusId = p.StatusId,
        PriorityId = p.PriorityId,
        StartDate = p.StartDate,
        PlannedEndDate = p.PlannedEndDate,
        ActualEndDate = p.ActualEndDate,
        CompletionPercentage = p.CompletionPercentage,
        Budget = p.Budget,
        CurrencyCode = p.CurrencyCode,
        IsArchived = p.IsArchived,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
        RowVersion = p.RowVersion is null ? string.Empty : Convert.ToBase64String(p.RowVersion)
    };
}
