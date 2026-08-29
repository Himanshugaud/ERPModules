using FluentValidation;

namespace ERP.Application.Projects;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CurrencyCode).Length(3).When(x => !string.IsNullOrEmpty(x.CurrencyCode));
        RuleFor(x => x.Budget).GreaterThanOrEqualTo(0).When(x => x.Budget.HasValue);
        RuleFor(x => x.PlannedEndDate)
            .GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.StartDate.HasValue && x.PlannedEndDate.HasValue)
            .WithMessage("PlannedEndDate must be on or after StartDate.");
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CurrencyCode).Length(3).When(x => !string.IsNullOrEmpty(x.CurrencyCode));
        RuleFor(x => x.Budget).GreaterThanOrEqualTo(0).When(x => x.Budget.HasValue);
        RuleFor(x => x.CompletionPercentage)
            .InclusiveBetween(0, 100).When(x => x.CompletionPercentage.HasValue);
        RuleFor(x => x.PlannedEndDate)
            .GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.StartDate.HasValue && x.PlannedEndDate.HasValue)
            .WithMessage("PlannedEndDate must be on or after StartDate.");
    }
}
