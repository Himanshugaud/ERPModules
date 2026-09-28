using ERP.Domain.Constants;
using FluentValidation;

namespace ERP.Application.Inventory;

public sealed class CreateMaterialRequirementRequestValidator : AbstractValidator<CreateMaterialRequirementRequest>
{
    public CreateMaterialRequirementRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.DestinationAddress).MaximumLength(300);
        RuleFor(x => x)
            .Must(x => x.WarehouseId.HasValue || !string.IsNullOrWhiteSpace(x.DestinationAddress))
            .WithMessage("Provide a destination: either a site address or a linked warehouse.");
        RuleFor(x => x.Priority).Must(p => Priorities.All.Contains(p!))
            .When(x => !string.IsNullOrEmpty(x.Priority))
            .WithMessage($"Priority must be one of: {string.Join(", ", Priorities.All)}");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.Qty).GreaterThan(0);
        });
    }
}

public sealed class RejectMaterialRequirementRequestValidator : AbstractValidator<RejectMaterialRequirementRequest>
{
    public RejectMaterialRequirementRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ConvertToPurchaseOrderRequestValidator : AbstractValidator<ConvertToPurchaseOrderRequest>
{
    public ConvertToPurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.MaterialRequirementLineId).NotEmpty();
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.TaxRatePercent).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.Qty).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.TaxRatePercent).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class UpdatePurchaseOrderRequestValidator : AbstractValidator<UpdatePurchaseOrderRequest>
{
    public UpdatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.Qty).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.TaxRatePercent).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class RejectPurchaseOrderRequestValidator : AbstractValidator<RejectPurchaseOrderRequest>
{
    public RejectPurchaseOrderRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
