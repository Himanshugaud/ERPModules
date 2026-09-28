using ERP.Domain.Constants;
using FluentValidation;

namespace ERP.Application.Inventory;

public sealed class CreateItemRequestValidator : AbstractValidator<CreateItemRequest>
{
    public CreateItemRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ItemType).NotEmpty().Must(t => ItemTypes.All.Contains(t))
            .WithMessage($"ItemType must be one of: {string.Join(", ", ItemTypes.All)}");
        RuleFor(x => x.StandardCost).GreaterThanOrEqualTo(0).When(x => x.StandardCost.HasValue);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0).When(x => x.ReorderLevel.HasValue);
    }
}

public sealed class UpdateItemRequestValidator : AbstractValidator<UpdateItemRequest>
{
    public UpdateItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ItemType).NotEmpty().Must(t => ItemTypes.All.Contains(t))
            .WithMessage($"ItemType must be one of: {string.Join(", ", ItemTypes.All)}");
        RuleFor(x => x.StandardCost).GreaterThanOrEqualTo(0).When(x => x.StandardCost.HasValue);
    }
}

public sealed class CreateWarehouseRequestValidator : AbstractValidator<CreateWarehouseRequest>
{
    public CreateWarehouseRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.WarehouseType).Must(t => WarehouseTypes.All.Contains(t!))
            .When(x => !string.IsNullOrEmpty(x.WarehouseType))
            .WithMessage($"WarehouseType must be one of: {string.Join(", ", WarehouseTypes.All)}");
    }
}

public sealed class UpdateWarehouseRequestValidator : AbstractValidator<UpdateWarehouseRequest>
{
    public UpdateWarehouseRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.WarehouseType).Must(t => WarehouseTypes.All.Contains(t!))
            .When(x => !string.IsNullOrEmpty(x.WarehouseType));
    }
}

public sealed class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
    }
}

public sealed class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
{
    public UpdateSupplierRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
    }
}

public sealed class CreateItemCategoryRequestValidator : AbstractValidator<CreateItemCategoryRequest>
{
    public CreateItemCategoryRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public sealed class CreateGoodsReceiptRequestValidator : AbstractValidator<CreateGoodsReceiptRequest>
{
    public CreateGoodsReceiptRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.PoReference).NotEmpty().MaximumLength(50).WithMessage("A purchase order reference is required for every goods receipt.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.Qty).GreaterThan(0);
            l.RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreateMaterialIssueRequestValidator : AbstractValidator<CreateMaterialIssueRequest>
{
    public CreateMaterialIssueRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.Qty).GreaterThan(0);
        });
    }
}

public sealed class CreateStockTransferRequestValidator : AbstractValidator<CreateStockTransferRequest>
{
    public CreateStockTransferRequestValidator()
    {
        RuleFor(x => x.SourceAddress).MaximumLength(300);
        RuleFor(x => x)
            .Must(x => x.FromWarehouseId.HasValue || !string.IsNullOrWhiteSpace(x.SourceAddress))
            .WithMessage("Provide a source: either a warehouse or a source address (e.g. a supplier).");
        RuleFor(x => x.ToWarehouseId).NotEmpty()
            .Must((req, toId) => !req.FromWarehouseId.HasValue || toId != req.FromWarehouseId.Value)
            .WithMessage("Source and destination warehouses must differ.");
        RuleFor(x => x.TransportId).NotEmpty().MaximumLength(50).WithMessage("A transport ID is required for every stock transfer.");
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.Qty).GreaterThan(0);
        });
    }
}

public sealed class CreateStockAdjustmentRequestValidator : AbstractValidator<CreateStockAdjustmentRequest>
{
    public CreateStockAdjustmentRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemId).NotEmpty();
            l.RuleFor(x => x.QtyDelta).NotEqual(0).WithMessage("QtyDelta cannot be zero.");
        });
    }
}

public sealed class CreateBomRequestValidator : AbstractValidator<CreateBomRequest>
{
    public CreateBomRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.OutputItemId).NotEmpty();
        RuleFor(x => x.OutputQty).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A BOM must have at least one component.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ComponentItemId).NotEmpty();
            l.RuleFor(x => x.Qty).GreaterThan(0);
            l.RuleFor(x => x.ScrapPercent).InclusiveBetween(0, 100);
        });
    }
}

public sealed class CreateWorkOrderRequestValidator : AbstractValidator<CreateWorkOrderRequest>
{
    public CreateWorkOrderRequestValidator()
    {
        RuleFor(x => x.OutputItemId).NotEmpty();
        RuleFor(x => x.PlannedQty).GreaterThan(0);
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}

public sealed class CompleteWorkOrderRequestValidator : AbstractValidator<CompleteWorkOrderRequest>
{
    public CompleteWorkOrderRequestValidator()
    {
        RuleFor(x => x.ProducedQty).GreaterThan(0);
        RuleFor(x => x.ScrapQty).GreaterThanOrEqualTo(0);
    }
}
