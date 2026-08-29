namespace ERP.Domain.Entities;

public class TaskItem
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? PriorityId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? ReporterId { get; set; }
    public Guid? MilestoneId { get; set; }
    public Guid? SprintId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public decimal CompletionPercentage { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = default!;
}

public class TaskStatus
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsFinal { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TaskPriority
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TaskWatcher
{
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Milestone
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Status { get; set; } = "OPEN";
    public Guid? OwnerId { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal CompletionPercentage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = default!;
}

public class Sprint
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = default!;
    public string? Goal { get; set; }
    public string Status { get; set; } = "PLANNED";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
