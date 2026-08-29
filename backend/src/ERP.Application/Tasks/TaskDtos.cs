using FluentValidation;

namespace ERP.Application.Tasks;

public sealed class CreateTaskRequest
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? ParentTaskId { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? PriorityId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? MilestoneId { get; set; }
    public Guid? SprintId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal? EstimatedHours { get; set; }
}

public sealed class UpdateTaskRequest
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? PriorityId { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? MilestoneId { get; set; }
    public Guid? SprintId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public decimal? CompletionPercentage { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class AssignTaskRequest
{
    public Guid? AssigneeId { get; set; }
}

public sealed class ChangeTaskStatusRequest
{
    public Guid StatusId { get; set; }
}

public sealed class TaskResponse
{
    public Guid Id { get; set; }
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string RowVersion { get; set; } = default!;
}

public sealed class TaskWatcherResponse
{
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.EstimatedHours).GreaterThanOrEqualTo(0).When(x => x.EstimatedHours.HasValue);
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.StartDate.HasValue && x.DueDate.HasValue)
            .WithMessage("DueDate must be on or after StartDate.");
    }
}

public sealed class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.EstimatedHours).GreaterThanOrEqualTo(0).When(x => x.EstimatedHours.HasValue);
        RuleFor(x => x.ActualHours).GreaterThanOrEqualTo(0).When(x => x.ActualHours.HasValue);
        RuleFor(x => x.CompletionPercentage).InclusiveBetween(0, 100).When(x => x.CompletionPercentage.HasValue);
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.StartDate.HasValue && x.DueDate.HasValue)
            .WithMessage("DueDate must be on or after StartDate.");
    }
}

public sealed class ChangeTaskStatusRequestValidator : AbstractValidator<ChangeTaskStatusRequest>
{
    public ChangeTaskStatusRequestValidator()
    {
        RuleFor(x => x.StatusId).NotEmpty();
    }
}
