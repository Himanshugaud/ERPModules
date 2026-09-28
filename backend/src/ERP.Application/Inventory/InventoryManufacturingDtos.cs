namespace ERP.Application.Inventory;

// ---- BOM ----
public sealed class BomLineRequest
{
    public Guid ComponentItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public decimal ScrapPercent { get; set; }
    public string? Operation { get; set; }
}

public sealed class CreateBomRequest
{
    public string Code { get; set; } = default!;
    public Guid OutputItemId { get; set; }
    public decimal OutputQty { get; set; } = 1;
    public Guid? UomId { get; set; }
    public int Version { get; set; } = 1;
    public List<BomLineRequest> Lines { get; set; } = new();
}

public sealed class BomLineResponse
{
    public Guid Id { get; set; }
    public Guid ComponentItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public decimal ScrapPercent { get; set; }
    public string? Operation { get; set; }
}

public sealed class BomResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public Guid OutputItemId { get; set; }
    public decimal OutputQty { get; set; }
    public Guid? UomId { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<BomLineResponse> Lines { get; set; } = new();
}

// ---- Work Orders ----
public sealed class CreateWorkOrderRequest
{
    public string? WoNumber { get; set; }
    public Guid OutputItemId { get; set; }
    public Guid? BomId { get; set; }
    public decimal PlannedQty { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateOnly? StartDate { get; set; }
}

public sealed class CompleteWorkOrderRequest
{
    public decimal ProducedQty { get; set; }
    public decimal ScrapQty { get; set; }
}

public sealed class WorkOrderComponentResponse
{
    public Guid Id { get; set; }
    public Guid ComponentItemId { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ConsumedQty { get; set; }
    public Guid? UomId { get; set; }
}

public sealed class WorkOrderResponse
{
    public Guid Id { get; set; }
    public string WoNumber { get; set; } = default!;
    public Guid OutputItemId { get; set; }
    public Guid? BomId { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ProducedQty { get; set; }
    public decimal ScrapQty { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Status { get; set; } = default!;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RowVersion { get; set; } = default!;
    public List<WorkOrderComponentResponse> Components { get; set; } = new();
}
