namespace ERP.Application.Inventory;

// ---- Items ----
public sealed class CreateItemRequest
{
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
    public string? ValuationMethod { get; set; }
    public decimal? StandardCost { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal? SafetyStock { get; set; }
    public decimal? MinStock { get; set; }
    public decimal? MaxStock { get; set; }
    public decimal? ReorderQty { get; set; }
    public bool IsPurchasable { get; set; } = true;
    public bool IsManufactured { get; set; }
    public bool IsSellable { get; set; }
}

public sealed class UpdateItemRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string ItemType { get; set; } = default!;
    public Guid? CategoryId { get; set; }
    public Guid? BaseUomId { get; set; }
    public string? Barcode { get; set; }
    public bool TrackBatches { get; set; }
    public bool TrackSerials { get; set; }
    public bool TrackExpiry { get; set; }
    public string? ValuationMethod { get; set; }
    public decimal? StandardCost { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal? SafetyStock { get; set; }
    public decimal? MinStock { get; set; }
    public decimal? MaxStock { get; set; }
    public decimal? ReorderQty { get; set; }
    public bool IsPurchasable { get; set; }
    public bool IsManufactured { get; set; }
    public bool IsSellable { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RowVersion { get; set; }
}

public sealed class ItemResponse
{
    public Guid Id { get; set; }
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
    public string ValuationMethod { get; set; } = default!;
    public decimal? StandardCost { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal? SafetyStock { get; set; }
    public decimal? MinStock { get; set; }
    public decimal? MaxStock { get; set; }
    public decimal? ReorderQty { get; set; }
    public bool IsPurchasable { get; set; }
    public bool IsManufactured { get; set; }
    public bool IsSellable { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string RowVersion { get; set; } = default!;
}

// ---- Warehouses ----
public sealed class CreateWarehouseRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? WarehouseType { get; set; }
    public Guid? ProjectId { get; set; }
    public string? Address { get; set; }
}

public sealed class UpdateWarehouseRequest
{
    public string Name { get; set; } = default!;
    public string? WarehouseType { get; set; }
    public Guid? ProjectId { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WarehouseResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string WarehouseType { get; set; } = default!;
    public Guid? ProjectId { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ---- Suppliers ----
public sealed class CreateSupplierRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public sealed class UpdateSupplierRequest
{
    public string Name { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Status { get; set; }
}

public sealed class SupplierResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

// ---- Lookups & stock reads ----
public sealed class ItemCategoryResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public Guid? ParentCategoryId { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateItemCategoryRequest
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? ParentCategoryId { get; set; }
}

public sealed class UomResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsBaseUnit { get; set; }
}

public sealed class StockLevelResponse
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BatchId { get; set; }
    public decimal QtyOnHand { get; set; }
    public decimal QtyReserved { get; set; }
    public decimal QtyAvailable { get; set; }
    public decimal QtyInTransit { get; set; }
    public decimal AvgUnitCost { get; set; }
    public decimal StockValue { get; set; }
}

public sealed class StockMovementResponse
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BatchId { get; set; }
    public string MovementType { get; set; } = default!;
    public string Direction { get; set; } = default!;
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? RefDocType { get; set; }
    public Guid? RefDocId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime OccurredAt { get; set; }
}

// ---- Transaction documents ----
public sealed class GoodsReceiptLineRequest
{
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public decimal UnitCost { get; set; }
    public string? BatchNo { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}

public sealed class CreateGoodsReceiptRequest
{
    public string? GrnNumber { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? PoReference { get; set; }
    public Guid WarehouseId { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    public string? Notes { get; set; }
    public List<GoodsReceiptLineRequest> Lines { get; set; } = new();
}

public sealed class MaterialIssueLineRequest
{
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public Guid? BatchId { get; set; }
}

public sealed class CreateMaterialIssueRequest
{
    public string? IssueNumber { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string? IssueType { get; set; }
    public DateOnly? IssueDate { get; set; }
    public string? Notes { get; set; }
    public List<MaterialIssueLineRequest> Lines { get; set; } = new();
}

public sealed class StockTransferLineRequest
{
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public Guid? BatchId { get; set; }
}

public sealed class CreateStockTransferRequest
{
    public string? TransferNumber { get; set; }
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public string? TransportId { get; set; }
    public DateOnly? TransferDate { get; set; }
    public string? Notes { get; set; }
    public List<StockTransferLineRequest> Lines { get; set; } = new();
}

public sealed class StockAdjustmentLineRequest
{
    public Guid ItemId { get; set; }
    public decimal QtyDelta { get; set; }
    public Guid? UomId { get; set; }
    public Guid? BatchId { get; set; }
    public decimal? UnitCost { get; set; }
}

public sealed class CreateStockAdjustmentRequest
{
    public string? AdjustmentNumber { get; set; }
    public Guid WarehouseId { get; set; }
    public string ReasonCode { get; set; } = default!;
    public DateOnly? AdjustmentDate { get; set; }
    public string? Notes { get; set; }
    public List<StockAdjustmentLineRequest> Lines { get; set; } = new();
}

public sealed class InventoryDocumentResponse
{
    public Guid Id { get; set; }
    public string Number { get; set; } = default!;
    public string DocumentType { get; set; } = default!;
    public Guid WarehouseId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? SupplierId { get; set; }
    public string Status { get; set; } = default!;
    public DateOnly? DocumentDate { get; set; }
    public string? Reference { get; set; }
    public int LineCount { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
