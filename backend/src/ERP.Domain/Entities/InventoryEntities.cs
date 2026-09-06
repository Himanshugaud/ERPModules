namespace ERP.Domain.Entities;

public class ItemCategory
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class UnitOfMeasure
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsBaseUnit { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class Item
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string ItemType { get; set; } = default!;
    public Guid? CategoryId { get; set; }
    public Guid? BaseUomId { get; set; }
    public string? Barcode { get; set; }
    public bool TrackBatches { get; set; }
    public bool TrackSerials { get; set; }
    public bool TrackExpiry { get; set; }
    public string ValuationMethod { get; set; } = "WEIGHTED_AVG";
    public decimal? StandardCost { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal? SafetyStock { get; set; }
    public decimal? MinStock { get; set; }
    public decimal? MaxStock { get; set; }
    public decimal? ReorderQty { get; set; }
    public bool IsPurchasable { get; set; } = true;
    public bool IsManufactured { get; set; }
    public bool IsSellable { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = default!;
}

public class Warehouse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string WarehouseType { get; set; } = "MAIN_STORE";
    public Guid? ProjectId { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class Supplier
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class Batch
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ItemId { get; set; }
    public string BatchNo { get; set; } = default!;
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public Guid? SupplierId { get; set; }
    public string QcStatus { get; set; } = "RELEASED";
    public DateTime CreatedAt { get; set; }
}

public class StockLevel
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BinId { get; set; }
    public Guid? BatchId { get; set; }
    public decimal QtyOnHand { get; set; }
    public decimal QtyReserved { get; set; }
    public decimal QtyInTransit { get; set; }
    public decimal AvgUnitCost { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = default!;
}

public class StockMovement
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BinId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? SerialId { get; set; }
    public string MovementType { get; set; } = default!;
    public string Direction { get; set; } = default!;
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? RefDocType { get; set; }
    public Guid? RefDocId { get; set; }
    public Guid? RefDocLineId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid? CreatedBy { get; set; }
}

public class GoodsReceipt
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string GrnNumber { get; set; } = default!;
    public Guid? SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? PoReference { get; set; }
    public Guid WarehouseId { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    public string Status { get; set; } = "POSTED";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public List<GoodsReceiptLine> Lines { get; set; } = new();
}

public class GoodsReceiptLine
{
    public Guid Id { get; set; }
    public Guid GoodsReceiptId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public decimal UnitCost { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? BinId { get; set; }
}

public class MaterialIssue
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string IssueNumber { get; set; } = default!;
    public Guid WarehouseId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string IssueType { get; set; } = "PROJECT";
    public DateOnly? IssueDate { get; set; }
    public string Status { get; set; } = "POSTED";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public List<MaterialIssueLine> Lines { get; set; } = new();
}

public class MaterialIssueLine
{
    public Guid Id { get; set; }
    public Guid MaterialIssueId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public Guid? BatchId { get; set; }
    public decimal UnitCost { get; set; }
}

public class StockTransfer
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string TransferNumber { get; set; } = default!;
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public string? TransportId { get; set; }
    public string Status { get; set; } = "POSTED";
    public DateOnly? TransferDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public List<StockTransferLine> Lines { get; set; } = new();
}

public class StockTransferLine
{
    public Guid Id { get; set; }
    public Guid StockTransferId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public Guid? BatchId { get; set; }
}

public class StockAdjustment
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string AdjustmentNumber { get; set; } = default!;
    public Guid WarehouseId { get; set; }
    public string ReasonCode { get; set; } = default!;
    public DateOnly? AdjustmentDate { get; set; }
    public string Status { get; set; } = "POSTED";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public List<StockAdjustmentLine> Lines { get; set; } = new();
}

public class StockAdjustmentLine
{
    public Guid Id { get; set; }
    public Guid StockAdjustmentId { get; set; }
    public Guid ItemId { get; set; }
    public decimal QtyDelta { get; set; }
    public Guid? UomId { get; set; }
    public Guid? BatchId { get; set; }
    public decimal UnitCost { get; set; }
}
