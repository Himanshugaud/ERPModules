using FluentValidation;

namespace ERP.Application.Projects;

public sealed class AddProjectMemberRequest
{
    public Guid UserId { get; set; }
    public string? ProjectRole { get; set; }
    public decimal? AllocationPercentage { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public sealed class UpdateProjectMemberRequest
{
    public string? ProjectRole { get; set; }
    public decimal? AllocationPercentage { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Status { get; set; }
}

public sealed class ProjectMemberResponse
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string? ProjectRole { get; set; }
    public decimal? AllocationPercentage { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = default!;
}

public sealed class AddProjectMemberRequestValidator : AbstractValidator<AddProjectMemberRequest>
{
    public AddProjectMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AllocationPercentage).InclusiveBetween(0, 100)
            .When(x => x.AllocationPercentage.HasValue);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);
    }
}

public sealed class UpdateProjectMemberRequestValidator : AbstractValidator<UpdateProjectMemberRequest>
{
    public UpdateProjectMemberRequestValidator()
    {
        RuleFor(x => x.AllocationPercentage).InclusiveBetween(0, 100)
            .When(x => x.AllocationPercentage.HasValue);
    }
}
