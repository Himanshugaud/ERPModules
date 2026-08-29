using ERP.Application.Abstractions;
using ERP.Domain.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Shared.Pagination;
using Microsoft.EntityFrameworkCore;
using TaskStatus = ERP.Domain.Entities.TaskStatus;

namespace ERP.Infrastructure.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly ErpDbContext _db;
    public TaskRepository(ErpDbContext db) => _db = db;

    public async Task<TaskItem?> GetByIdAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default)
    {
        var q = _db.Tasks.Where(t => t.OrganizationId == organizationId && t.Id == id);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<TaskItem>> ListByProjectAsync(Guid organizationId, Guid projectId, TaskFilter filter, CancellationToken ct = default)
    {
        var q = _db.Tasks.AsNoTracking().Where(t => t.OrganizationId == organizationId && t.ProjectId == projectId);

        if (filter.StatusId.HasValue) q = q.Where(t => t.StatusId == filter.StatusId);
        if (filter.PriorityId.HasValue) q = q.Where(t => t.PriorityId == filter.PriorityId);
        if (filter.AssigneeId.HasValue) q = q.Where(t => t.AssigneeId == filter.AssigneeId);
        if (filter.SprintId.HasValue) q = q.Where(t => t.SprintId == filter.SprintId);
        if (filter.MilestoneId.HasValue) q = q.Where(t => t.MilestoneId == filter.MilestoneId);
        if (filter.DueBefore.HasValue) q = q.Where(t => t.DueDate <= filter.DueBefore);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            q = q.Where(t => EF.Functions.Like(t.Title, $"%{term}%"));
        }

        var total = await q.LongCountAsync(ct);
        q = filter.Sort?.ToLowerInvariant() switch
        {
            "title" => q.OrderBy(t => t.Title),
            "-title" => q.OrderByDescending(t => t.Title),
            "duedate" => q.OrderBy(t => t.DueDate),
            "-duedate" => q.OrderByDescending(t => t.DueDate),
            _ => q.OrderByDescending(t => t.CreatedAt)
        };
        var items = await q.Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<TaskItem> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public async Task<IReadOnlyList<TaskItem>> ListSubtasksAsync(Guid organizationId, Guid parentTaskId, CancellationToken ct = default) =>
        await _db.Tasks.AsNoTracking()
            .Where(t => t.OrganizationId == organizationId && t.ParentTaskId == parentTaskId)
            .OrderBy(t => t.CreatedAt).ToListAsync(ct);

    public Task<bool> ExistsInProjectAsync(Guid organizationId, Guid projectId, Guid taskId, CancellationToken ct = default) =>
        _db.Tasks.AnyAsync(t => t.OrganizationId == organizationId && t.ProjectId == projectId && t.Id == taskId, ct);

    public async Task AddAsync(TaskItem task, CancellationToken ct = default) => await _db.Tasks.AddAsync(task, ct);
}

public sealed class TaskStatusRepository : ITaskStatusRepository
{
    private readonly ErpDbContext _db;
    public TaskStatusRepository(ErpDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid organizationId, Guid statusId, CancellationToken ct = default) =>
        _db.TaskStatuses.AnyAsync(s => s.OrganizationId == organizationId && s.Id == statusId, ct);

    public Task<TaskStatus?> GetAsync(Guid organizationId, Guid statusId, CancellationToken ct = default) =>
        _db.TaskStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.Id == statusId, ct);

    public async Task<IReadOnlyList<TaskStatus>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.TaskStatuses.AsNoTracking().Where(s => s.OrganizationId == organizationId)
            .OrderBy(s => s.DisplayOrder).ToListAsync(ct);
}

public sealed class TaskPriorityRepository : ITaskPriorityRepository
{
    private readonly ErpDbContext _db;
    public TaskPriorityRepository(ErpDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid organizationId, Guid priorityId, CancellationToken ct = default) =>
        _db.TaskPriorities.AnyAsync(p => p.OrganizationId == organizationId && p.Id == priorityId, ct);

    public async Task<IReadOnlyList<TaskPriority>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.TaskPriorities.AsNoTracking().Where(p => p.OrganizationId == organizationId)
            .OrderBy(p => p.DisplayOrder).ToListAsync(ct);
}

public sealed class TaskWatcherRepository : ITaskWatcherRepository
{
    private readonly ErpDbContext _db;
    public TaskWatcherRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<TaskWatcher>> ListAsync(Guid taskId, CancellationToken ct = default) =>
        await _db.TaskWatchers.AsNoTracking().Where(w => w.TaskId == taskId).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid taskId, Guid userId, CancellationToken ct = default) =>
        _db.TaskWatchers.AnyAsync(w => w.TaskId == taskId && w.UserId == userId, ct);

    public async Task AddAsync(TaskWatcher watcher, CancellationToken ct = default) => await _db.TaskWatchers.AddAsync(watcher, ct);

    public async Task RemoveAsync(Guid taskId, Guid userId, CancellationToken ct = default)
    {
        var link = await _db.TaskWatchers.FirstOrDefaultAsync(w => w.TaskId == taskId && w.UserId == userId, ct);
        if (link is not null) _db.TaskWatchers.Remove(link);
    }
}

public sealed class MilestoneRepository : IMilestoneRepository
{
    private readonly ErpDbContext _db;
    public MilestoneRepository(ErpDbContext db) => _db = db;

    public Task<bool> ExistsInProjectAsync(Guid organizationId, Guid projectId, Guid milestoneId, CancellationToken ct = default) =>
        _db.Milestones.AnyAsync(m => m.OrganizationId == organizationId && m.ProjectId == projectId && m.Id == milestoneId, ct);
}

public sealed class SprintRepository : ISprintRepository
{
    private readonly ErpDbContext _db;
    public SprintRepository(ErpDbContext db) => _db = db;

    public Task<bool> ExistsInProjectAsync(Guid organizationId, Guid projectId, Guid sprintId, CancellationToken ct = default) =>
        _db.Sprints.AnyAsync(s => s.OrganizationId == organizationId && s.ProjectId == projectId && s.Id == sprintId, ct);
}
