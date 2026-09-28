namespace ERP.Domain.Entities;

public class MaterialRequirement
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string ReqNumber { get; set; } = default!;
    public Guid ProjectId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? DestinationAddress { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Priority { get; set; } = "MEDIUM";
    public string Status { get; set; } = "SUBMITTED";
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
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = default!;
    public List<MaterialRequirementLine> Lines { get; set; } = new();
}

public class MaterialRequirementLine
{
    public Guid Id { get; set; }
    public Guid MaterialRequirementId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public string? Notes { get; set; }
    public Guid? PurchaseOrderLineId { get; set; }
}

public class PurchaseOrder
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string PoNumber { get; set; } = default!;
    public Guid SupplierId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? MaterialRequirementId { get; set; }
    public string Status { get; set; } = "DRAFT";
    public DateOnly? OrderDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = default!;
    public List<PurchaseOrderLine> Lines { get; set; } = new();
}

public class PurchaseOrderLine
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
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
