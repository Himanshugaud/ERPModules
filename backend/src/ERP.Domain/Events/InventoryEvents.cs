namespace ERP.Domain.Events;

public record ItemCreated(Guid ItemId, string Code, string Name, string ItemType) : IntegrationEvent
{
    public override string EventType => "ItemCreated";
    public override string AggregateType => "Item";
    public override Guid AggregateId => ItemId;
}

public record StockReceived(Guid ItemId, Guid WarehouseId, decimal Qty, decimal UnitCost) : IntegrationEvent
{
    public override string EventType => "StockReceived";
    public override string AggregateType => "Item";
    public override Guid AggregateId => ItemId;
}

public record StockIssued(Guid ItemId, Guid WarehouseId, decimal Qty, Guid? ProjectId) : IntegrationEvent
{
    public override string EventType => "StockIssued";
    public override string AggregateType => "Item";
    public override Guid AggregateId => ItemId;
}

public record StockTransferred(Guid ItemId, Guid FromWarehouseId, Guid ToWarehouseId, decimal Qty) : IntegrationEvent
{
    public override string EventType => "StockTransferred";
    public override string AggregateType => "Item";
    public override Guid AggregateId => ItemId;
}

public record StockAdjusted(Guid ItemId, Guid WarehouseId, decimal QtyDelta, string ReasonCode) : IntegrationEvent
{
    public override string EventType => "StockAdjusted";
    public override string AggregateType => "Item";
    public override Guid AggregateId => ItemId;
}

public record ReorderLevelBreached(Guid ItemId, Guid WarehouseId, decimal QtyOnHand, decimal ReorderLevel) : IntegrationEvent
{
    public override string EventType => "ReorderLevelBreached";
    public override string AggregateType => "Item";
    public override Guid AggregateId => ItemId;
}
