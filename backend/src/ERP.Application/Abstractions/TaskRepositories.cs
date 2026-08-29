using ERP.Domain.Entities;
using ERP.Shared.Pagination;
using TaskStatus = ERP.Domain.Entities.TaskStatus;

namespace ERP.Application.Abstractions;

public sealed class TaskFilter : PageRequest
{
    public Guid? StatusId { get; set; }
    public Guid? PriorityId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid? MilestoneId { get; set; }
    public DateOnly? DueBefore { get; set; }
    public string? Search { get; set; }
}

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default);
    Task<PagedResult<TaskItem>> ListByProjectAsync(Guid organizationId, Guid projectId, TaskFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> ListSubtasksAsync(Guid organizationId, Guid parentTaskId, CancellationToken ct = default);
    Task<bool> ExistsInProjectAsync(Guid organizationId, Guid projectId, Guid taskId, CancellationToken ct = default);
    Task AddAsync(TaskItem task, CancellationToken ct = default);
}

public interface ITaskStatusRepository
{
    Task<bool> ExistsAsync(Guid organizationId, Guid statusId, CancellationToken ct = default);
    Task<TaskStatus?> GetAsync(Guid organizationId, Guid statusId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskStatus>> ListAsync(Guid organizationId, CancellationToken ct = default);
}

public interface ITaskPriorityRepository
{
    Task<bool> ExistsAsync(Guid organizationId, Guid priorityId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskPriority>> ListAsync(Guid organizationId, CancellationToken ct = default);
}

public interface ITaskWatcherRepository
{
    Task<IReadOnlyList<TaskWatcher>> ListAsync(Guid taskId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid taskId, Guid userId, CancellationToken ct = default);
    Task AddAsync(TaskWatcher watcher, CancellationToken ct = default);
    Task RemoveAsync(Guid taskId, Guid userId, CancellationToken ct = default);
}

public interface IMilestoneRepository
{
    Task<bool> ExistsInProjectAsync(Guid organizationId, Guid projectId, Guid milestoneId, CancellationToken ct = default);
}

public interface ISprintRepository
{
    Task<bool> ExistsInProjectAsync(Guid organizationId, Guid projectId, Guid sprintId, CancellationToken ct = default);
}
