namespace ERP.Application.Inventory;

// ---- Material Requirements ----
public sealed class MaterialRequirementLineRequest
{
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreateMaterialRequirementRequest
{
    public string? ReqNumber { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? DestinationAddress { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? Priority { get; set; }
    public DateOnly? RequiredDate { get; set; }
    public string? Notes { get; set; }
    public List<MaterialRequirementLineRequest> Lines { get; set; } = new();
}

public sealed class RejectMaterialRequirementRequest
{
    public string Reason { get; set; } = default!;
}

public sealed class ConvertRequirementLineRequest
{
    public Guid MaterialRequirementLineId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRatePercent { get; set; }
}

public sealed class ConvertToPurchaseOrderRequest
{
    public Guid SupplierId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }
    public List<ConvertRequirementLineRequest> Lines { get; set; } = new();
}

public sealed class MaterialRequirementLineResponse
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public string? Notes { get; set; }
    public Guid? PurchaseOrderLineId { get; set; }
}

public sealed class MaterialRequirementResponse
{
    public Guid Id { get; set; }
    public string ReqNumber { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? DestinationAddress { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Priority { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateOnly? RequiredDate { get; set; }
    public Guid? RequestedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string RowVersion { get; set; } = default!;
    public List<MaterialRequirementLineResponse> Lines { get; set; } = new();
}

// ---- Purchase Orders ----
public sealed class PurchaseOrderLineRequest
{
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRatePercent { get; set; }
}

public sealed class CreatePurchaseOrderRequest
{
    public string? PoNumber { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateOnly? OrderDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLineRequest> Lines { get; set; } = new();
}

public sealed class UpdatePurchaseOrderRequest
{
    public Guid SupplierId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateOnly? OrderDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLineRequest> Lines { get; set; } = new();
    public string? RowVersion { get; set; }
}

public sealed class RejectPurchaseOrderRequest
{
    public string Reason { get; set; } = default!;
}

public sealed class PurchaseOrderLineResponse
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal QtyReceived { get; set; }
    public Guid? MaterialRequirementLineId { get; set; }
}

public sealed class PurchaseOrderResponse
{
    public Guid Id { get; set; }
    public string PoNumber { get; set; } = default!;
    public Guid SupplierId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? MaterialRequirementId { get; set; }
    public string Status { get; set; } = default!;
    public DateOnly? OrderDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string RowVersion { get; set; } = default!;
    public List<PurchaseOrderLineResponse> Lines { get; set; } = new();
}
