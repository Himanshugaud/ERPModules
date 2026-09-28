namespace ERP.Domain.Entities;

public class BillOfMaterials
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = default!;
    public Guid OutputItemId { get; set; }
    public decimal OutputQty { get; set; } = 1;
    public Guid? UomId { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public List<BomLine> Lines { get; set; } = new();
}

public class BomLine
{
    public Guid Id { get; set; }
    public Guid BillOfMaterialsId { get; set; }
    public Guid ComponentItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? UomId { get; set; }
    public decimal ScrapPercent { get; set; }
    public string? Operation { get; set; }
}

public class WorkOrder
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string WoNumber { get; set; } = default!;
    public Guid OutputItemId { get; set; }
    public Guid? BomId { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ProducedQty { get; set; }
    public decimal ScrapQty { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Status { get; set; } = "DRAFT";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = default!;
    public List<WorkOrderComponent> Components { get; set; } = new();
}

public class WorkOrderComponent
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid ComponentItemId { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ConsumedQty { get; set; }
    public Guid? UomId { get; set; }
    public Guid? BatchId { get; set; }
}
