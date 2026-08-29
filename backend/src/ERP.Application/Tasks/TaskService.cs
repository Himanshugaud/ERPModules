using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Domain.Events;
using ERP.Shared.Exceptions;
using ERP.Shared.Pagination;

namespace ERP.Application.Tasks;

public interface ITaskService
{
    Task<TaskResponse> CreateAsync(Guid projectId, CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskResponse> CreateSubtaskAsync(Guid parentTaskId, CreateTaskRequest request, CancellationToken ct = default);
    Task<PagedResult<TaskResponse>> ListAsync(Guid projectId, TaskFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<TaskResponse>> ListSubtasksAsync(Guid taskId, CancellationToken ct = default);
    Task<TaskResponse> GetAsync(Guid taskId, CancellationToken ct = default);
    Task<TaskResponse> UpdateAsync(Guid taskId, UpdateTaskRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid taskId, CancellationToken ct = default);
    Task<TaskResponse> AssignAsync(Guid taskId, AssignTaskRequest request, CancellationToken ct = default);
    Task<TaskResponse> ChangeStatusAsync(Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TaskWatcherResponse>> ListWatchersAsync(Guid taskId, CancellationToken ct = default);
    Task AddWatcherAsync(Guid taskId, Guid userId, CancellationToken ct = default);
    Task RemoveWatcherAsync(Guid taskId, Guid userId, CancellationToken ct = default);
}

public sealed class TaskService : ITaskService
{
    private readonly ITaskRepository _tasks;
    private readonly ITaskStatusRepository _statuses;
    private readonly ITaskPriorityRepository _priorities;
    private readonly ITaskWatcherRepository _watchers;
    private readonly IProjectRepository _projects;
    private readonly IProjectMemberRepository _members;
    private readonly IUserRepository _users;
    private readonly IMilestoneRepository _milestones;
    private readonly ISprintRepository _sprints;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IOutboxWriter _outbox;
    private readonly IClock _clock;

    public TaskService(ITaskRepository tasks, ITaskStatusRepository statuses, ITaskPriorityRepository priorities,
        ITaskWatcherRepository watchers, IProjectRepository projects, IProjectMemberRepository members,
        IUserRepository users, IMilestoneRepository milestones, ISprintRepository sprints,
        ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit, IOutboxWriter outbox, IClock clock)
    {
        _tasks = tasks;
        _statuses = statuses;
        _priorities = priorities;
        _watchers = watchers;
        _projects = projects;
        _members = members;
        _users = users;
        _milestones = milestones;
        _sprints = sprints;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<TaskResponse> CreateAsync(Guid projectId, CreateTaskRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var project = await _projects.GetByIdAsync(orgId, projectId, false, ct)
            ?? throw NotFoundException.For("Project", projectId);
        if (project.IsArchived)
            throw new ConflictException("Archived projects cannot receive new tasks.");

        if (request.ParentTaskId.HasValue &&
            !await _tasks.ExistsInProjectAsync(orgId, projectId, request.ParentTaskId.Value, ct))
            throw new ConflictException("Parent task must belong to the same project.");

        await ValidateReferencesAsync(project, request.StatusId, request.PriorityId, request.AssigneeId,
            request.MilestoneId, request.SprintId, ct);

        var now = _clock.UtcNow;
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ProjectId = projectId,
            ParentTaskId = request.ParentTaskId,
            Title = request.Title,
            Description = request.Description,
            StatusId = request.StatusId,
            PriorityId = request.PriorityId,
            AssigneeId = request.AssigneeId,
            ReporterId = _tenant.UserId,
            MilestoneId = request.MilestoneId,
            SprintId = request.SprintId,
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            EstimatedHours = request.EstimatedHours,
            CompletionPercentage = 0,
            CreatedAt = now,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _tasks.AddAsync(task, token);
            _audit.Add(EntityTypes.Task, task.Id, AuditActions.Create, null, new { task.Title, task.ProjectId });
            _outbox.Enqueue(new TaskCreated(projectId, task.Id, task.Title) { OrganizationId = orgId });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(task);
    }

    public async Task<TaskResponse> CreateSubtaskAsync(Guid parentTaskId, CreateTaskRequest request, CancellationToken ct = default)
    {
        var parent = await _tasks.GetByIdAsync(_tenant.OrganizationId, parentTaskId, false, ct)
            ?? throw NotFoundException.For("Task", parentTaskId);
        request.ParentTaskId = parentTaskId;
        return await CreateAsync(parent.ProjectId, request, ct);
    }

    public async Task<PagedResult<TaskResponse>> ListAsync(Guid projectId, TaskFilter filter, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        _ = await _projects.GetByIdAsync(orgId, projectId, false, ct)
            ?? throw NotFoundException.For("Project", projectId);

        var result = await _tasks.ListByProjectAsync(orgId, projectId, filter, ct);
        return new PagedResult<TaskResponse>
        {
            Items = result.Items.Select(Map).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<IReadOnlyList<TaskResponse>> ListSubtasksAsync(Guid taskId, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        _ = await _tasks.GetByIdAsync(orgId, taskId, false, ct) ?? throw NotFoundException.For("Task", taskId);
        var items = await _tasks.ListSubtasksAsync(orgId, taskId, ct);
        return items.Select(Map).ToList();
    }

    public async Task<TaskResponse> GetAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _tasks.GetByIdAsync(_tenant.OrganizationId, taskId, false, ct)
            ?? throw NotFoundException.For("Task", taskId);
        return Map(task);
    }

    public async Task<TaskResponse> UpdateAsync(Guid taskId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var task = await _tasks.GetByIdAsync(orgId, taskId, true, ct)
            ?? throw NotFoundException.For("Task", taskId);
        if (task.IsArchived)
            throw new ConflictException("Archived tasks cannot be modified.");

        var project = await _projects.GetByIdAsync(orgId, task.ProjectId, false, ct)
            ?? throw NotFoundException.For("Project", task.ProjectId);
        await ValidateReferencesAsync(project, request.StatusId, request.PriorityId, null,
            request.MilestoneId, request.SprintId, ct);

        if (!string.IsNullOrEmpty(request.RowVersion))
            task.RowVersion = Convert.FromBase64String(request.RowVersion);

        task.Title = request.Title;
        task.Description = request.Description;
        task.PriorityId = request.PriorityId;
        task.StatusId = request.StatusId;
        task.MilestoneId = request.MilestoneId;
        task.SprintId = request.SprintId;
        task.StartDate = request.StartDate;
        task.DueDate = request.DueDate;
        task.EstimatedHours = request.EstimatedHours;
        task.ActualHours = request.ActualHours;
        if (request.CompletionPercentage.HasValue) task.CompletionPercentage = request.CompletionPercentage.Value;
        task.UpdatedAt = _clock.UtcNow;
        task.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.Task, task.Id, AuditActions.Update, null, new { task.Title });
            _outbox.Enqueue(new TaskUpdated(task.ProjectId, task.Id) { OrganizationId = orgId });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(task);
    }

    public async Task DeleteAsync(Guid taskId, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var task = await _tasks.GetByIdAsync(orgId, taskId, true, ct)
            ?? throw NotFoundException.For("Task", taskId);

        task.IsDeleted = true;
        task.DeletedAt = _clock.UtcNow;
        task.DeletedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.Task, task.Id, AuditActions.Delete);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    public async Task<TaskResponse> AssignAsync(Guid taskId, AssignTaskRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var task = await _tasks.GetByIdAsync(orgId, taskId, true, ct)
            ?? throw NotFoundException.For("Task", taskId);
        var project = await _projects.GetByIdAsync(orgId, task.ProjectId, false, ct)
            ?? throw NotFoundException.For("Project", task.ProjectId);

        if (request.AssigneeId.HasValue)
            await EnsureAssigneeAccessAsync(project, request.AssigneeId.Value, ct);

        task.AssigneeId = request.AssigneeId;
        task.UpdatedAt = _clock.UtcNow;
        task.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.Task, task.Id, AuditActions.Assign, null, new { request.AssigneeId });
            _outbox.Enqueue(new TaskAssigned(task.ProjectId, task.Id, request.AssigneeId) { OrganizationId = orgId });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(task);
    }

    public async Task<TaskResponse> ChangeStatusAsync(Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var task = await _tasks.GetByIdAsync(orgId, taskId, true, ct)
            ?? throw NotFoundException.For("Task", taskId);

        var status = await _statuses.GetAsync(orgId, request.StatusId, ct)
            ?? throw new ConflictException("Status does not belong to the organization.");

        task.StatusId = status.Id;
        var completed = status.IsFinal;
        if (completed) task.CompletionPercentage = 100;
        task.UpdatedAt = _clock.UtcNow;
        task.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.Task, task.Id, AuditActions.StatusChange, null, new { status.Code });
            _outbox.Enqueue(new TaskStatusChanged(task.ProjectId, task.Id, status.Id) { OrganizationId = orgId });
            if (completed)
                _outbox.Enqueue(new TaskCompleted(task.ProjectId, task.Id) { OrganizationId = orgId });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(task);
    }

    public async Task<IReadOnlyList<TaskWatcherResponse>> ListWatchersAsync(Guid taskId, CancellationToken ct = default)
    {
        await EnsureTaskAsync(taskId, ct);
        var watchers = await _watchers.ListAsync(taskId, ct);
        return watchers.Select(w => new TaskWatcherResponse { TaskId = w.TaskId, UserId = w.UserId, CreatedAt = w.CreatedAt }).ToList();
    }

    public async Task AddWatcherAsync(Guid taskId, Guid userId, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        await EnsureTaskAsync(taskId, ct);
        if (!await _users.ExistsAsync(orgId, userId, ct))
            throw new ConflictException("User does not belong to the organization.");
        if (await _watchers.ExistsAsync(taskId, userId, ct))
            return;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _watchers.AddAsync(new TaskWatcher { TaskId = taskId, UserId = userId, CreatedAt = _clock.UtcNow }, token);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    public async Task RemoveWatcherAsync(Guid taskId, Guid userId, CancellationToken ct = default)
    {
        await EnsureTaskAsync(taskId, ct);
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _watchers.RemoveAsync(taskId, userId, token);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    private async Task EnsureTaskAsync(Guid taskId, CancellationToken ct)
    {
        _ = await _tasks.GetByIdAsync(_tenant.OrganizationId, taskId, false, ct)
            ?? throw NotFoundException.For("Task", taskId);
    }

    private async Task ValidateReferencesAsync(Project project, Guid? statusId, Guid? priorityId, Guid? assigneeId,
        Guid? milestoneId, Guid? sprintId, CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId;
        if (statusId.HasValue && !await _statuses.ExistsAsync(orgId, statusId.Value, ct))
            throw new ConflictException("Status does not belong to the organization.");
        if (priorityId.HasValue && !await _priorities.ExistsAsync(orgId, priorityId.Value, ct))
            throw new ConflictException("Priority does not belong to the organization.");
        if (milestoneId.HasValue && !await _milestones.ExistsInProjectAsync(orgId, project.Id, milestoneId.Value, ct))
            throw new ConflictException("Milestone must belong to the project.");
        if (sprintId.HasValue && !await _sprints.ExistsInProjectAsync(orgId, project.Id, sprintId.Value, ct))
            throw new ConflictException("Sprint must belong to the project.");
        if (assigneeId.HasValue)
            await EnsureAssigneeAccessAsync(project, assigneeId.Value, ct);
    }

    private async Task EnsureAssigneeAccessAsync(Project project, Guid assigneeId, CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId;
        if (!await _users.ExistsAsync(orgId, assigneeId, ct))
            throw new ConflictException("Assignee does not belong to the organization.");
        var hasAccess = project.ManagerId == assigneeId
            || await _members.ExistsAsync(project.Id, assigneeId, ct);
        if (!hasAccess)
            throw new ConflictException("Assignee must have access to the project.");
    }

    private static TaskResponse Map(TaskItem t) => new()
    {
        Id = t.Id,
        ProjectId = t.ProjectId,
        ParentTaskId = t.ParentTaskId,
        Title = t.Title,
        Description = t.Description,
        StatusId = t.StatusId,
        PriorityId = t.PriorityId,
        AssigneeId = t.AssigneeId,
        ReporterId = t.ReporterId,
        MilestoneId = t.MilestoneId,
        SprintId = t.SprintId,
        StartDate = t.StartDate,
        DueDate = t.DueDate,
        EstimatedHours = t.EstimatedHours,
        ActualHours = t.ActualHours,
        CompletionPercentage = t.CompletionPercentage,
        IsArchived = t.IsArchived,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        RowVersion = t.RowVersion is null ? string.Empty : Convert.ToBase64String(t.RowVersion)
    };
}
