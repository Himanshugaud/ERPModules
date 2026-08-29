using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Application.Abstractions;
using ERP.Application.Tasks;
using ERP.Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class TasksFunctions
{
    private readonly ITaskService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateTaskRequest> _createValidator;
    private readonly IValidator<UpdateTaskRequest> _updateValidator;
    private readonly IValidator<ChangeTaskStatusRequest> _statusValidator;

    public TasksFunctions(ITaskService service, IAuthorizationGuard auth,
        IValidator<CreateTaskRequest> createValidator, IValidator<UpdateTaskRequest> updateValidator,
        IValidator<ChangeTaskStatusRequest> statusValidator)
    {
        _service = service;
        _auth = auth;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
    }

    [Function("CreateTask")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/projects/{projectId:guid}/tasks")] HttpRequest req,
        Guid projectId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskCreate);
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        return Http.Created(await _service.CreateAsync(projectId, body, ct));
    }

    [Function("ListProjectTasks")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/tasks")] HttpRequest req,
        Guid projectId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskRead);
        var filter = new TaskFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            Sort = Http.StringQuery(req, "sort"),
            StatusId = Http.GuidQuery(req, "statusId"),
            PriorityId = Http.GuidQuery(req, "priorityId"),
            AssigneeId = Http.GuidQuery(req, "assigneeId"),
            SprintId = Http.GuidQuery(req, "sprintId"),
            MilestoneId = Http.GuidQuery(req, "milestoneId"),
            DueBefore = Http.DateQuery(req, "dueBefore"),
            Search = Http.StringQuery(req, "search")
        };
        return Http.Paged(await _service.ListAsync(projectId, filter, ct));
    }

    [Function("GetTask")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tasks/{taskId:guid}")] HttpRequest req,
        Guid taskId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskRead);
        return Http.Ok(await _service.GetAsync(taskId, ct));
    }

    [Function("UpdateTask")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tasks/{taskId:guid}")] HttpRequest req,
        Guid taskId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskUpdate);
        var body = await Http.ReadValidatedAsync(req, _updateValidator, ct);
        return Http.Ok(await _service.UpdateAsync(taskId, body, ct));
    }

    [Function("DeleteTask")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/tasks/{taskId:guid}")] HttpRequest req,
        Guid taskId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskDelete);
        await _service.DeleteAsync(taskId, ct);
        return Http.NoContent();
    }

    [Function("ListSubtasks")]
    public async Task<IActionResult> ListSubtasks(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tasks/{taskId:guid}/subtasks")] HttpRequest req,
        Guid taskId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskRead);
        return Http.Ok(await _service.ListSubtasksAsync(taskId, ct));
    }

    [Function("CreateSubtask")]
    public async Task<IActionResult> CreateSubtask(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tasks/{taskId:guid}/subtasks")] HttpRequest req,
        Guid taskId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskCreate);
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        return Http.Created(await _service.CreateSubtaskAsync(taskId, body, ct));
    }

    [Function("AssignTask")]
    public async Task<IActionResult> Assign(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tasks/{taskId:guid}/assignee")] HttpRequest req,
        Guid taskId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskAssign);
        var body = await Http.ReadAsync<AssignTaskRequest>(req, ct);
        return Http.Ok(await _service.AssignAsync(taskId, body, ct));
    }

    [Function("ChangeTaskStatus")]
    public async Task<IActionResult> ChangeStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tasks/{taskId:guid}/status")] HttpRequest req,
        Guid taskId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskUpdate);
        var body = await Http.ReadValidatedAsync(req, _statusValidator, ct);
        return Http.Ok(await _service.ChangeStatusAsync(taskId, body, ct));
    }

    [Function("ListTaskWatchers")]
    public async Task<IActionResult> ListWatchers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tasks/{taskId:guid}/watchers")] HttpRequest req,
        Guid taskId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskRead);
        return Http.Ok(await _service.ListWatchersAsync(taskId, ct));
    }

    [Function("AddTaskWatcher")]
    public async Task<IActionResult> AddWatcher(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tasks/{taskId:guid}/watchers/{userId:guid}")] HttpRequest req,
        Guid taskId, Guid userId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskUpdate);
        await _service.AddWatcherAsync(taskId, userId, ct);
        return Http.NoContent();
    }

    [Function("RemoveTaskWatcher")]
    public async Task<IActionResult> RemoveWatcher(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/tasks/{taskId:guid}/watchers/{userId:guid}")] HttpRequest req,
        Guid taskId, Guid userId, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskUpdate);
        await _service.RemoveWatcherAsync(taskId, userId, ct);
        return Http.NoContent();
    }
}

public sealed class TaskLookupsFunctions
{
    private readonly ITaskStatusRepository _statuses;
    private readonly ITaskPriorityRepository _priorities;
    private readonly ITenantContext _tenant;
    private readonly IAuthorizationGuard _auth;

    public TaskLookupsFunctions(ITaskStatusRepository statuses, ITaskPriorityRepository priorities,
        ITenantContext tenant, IAuthorizationGuard auth)
    {
        _statuses = statuses;
        _priorities = priorities;
        _tenant = tenant;
        _auth = auth;
    }

    [Function("ListTaskStatuses")]
    public async Task<IActionResult> Statuses(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/task-statuses")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskRead);
        var items = await _statuses.ListAsync(_tenant.OrganizationId, ct);
        return Http.Ok(items.Select(s => new { s.Id, s.Code, s.Name, s.DisplayOrder, s.IsFinal, s.IsActive }));
    }

    [Function("ListTaskPriorities")]
    public async Task<IActionResult> Priorities(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/task-priorities")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.TaskRead);
        var items = await _priorities.ListAsync(_tenant.OrganizationId, ct);
        return Http.Ok(items.Select(p => new { p.Id, p.Code, p.Name, p.DisplayOrder, p.IsActive }));
    }
}
