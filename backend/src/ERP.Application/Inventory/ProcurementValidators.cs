using ERP.Domain.Constants;
using FluentValidation;

namespace ERP.Application.Inventory;

public sealed class CreateMaterialRequirementRequestValidator : AbstractValidator<CreateMaterialRequirementRequest>
{
    public CreateMaterialRequirementRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.DestinationAddress).MaximumLength(300);
        RuleFor(x => x.SheetType).MaximumLength(40);
        RuleFor(x => x.BlockName).MaximumLength(100);
        RuleFor(x => x.BlockSize).MaximumLength(100);
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
            l.RuleFor(x => x.SectionName).MaximumLength(150);
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
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleFor(x => x.Lines).Must(lines => lines.Select(line => line.MaterialRequirementLineId).Distinct().Count() == lines.Count)
            .WithMessage("Each material request line can appear only once.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.MaterialRequirementLineId).NotEmpty();
            l.RuleFor(x => x.Qty).GreaterThan(0).When(x => x.Qty.HasValue);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.TaxRatePercent).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderRequestValidator()
    {
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

public sealed class MarkPurchaseOrderOrderedRequestValidator : AbstractValidator<MarkPurchaseOrderOrderedRequest>
{
    public MarkPurchaseOrderOrderedRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.CurrencyCode).Length(3).When(x => !string.IsNullOrEmpty(x.CurrencyCode));
        RuleFor(x => x.Lines).NotEmpty();
        RuleFor(x => x.Lines).Must(lines => lines.Select(line => line.LineId).Distinct().Count() == lines.Count)
            .WithMessage("Each purchase order line must be priced exactly once.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.LineId).NotEmpty();
            line.RuleFor(x => x.UnitPrice).GreaterThan(0);
            line.RuleFor(x => x.TaxRatePercent).InclusiveBetween(0, 100);
        });
    }
}
