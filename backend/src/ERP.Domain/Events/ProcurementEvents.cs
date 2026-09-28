namespace ERP.Domain.Events;

public record MaterialRequirementApproved(Guid MaterialRequirementId, Guid ProjectId) : IntegrationEvent
{
    public override string EventType => "MaterialRequirementApproved";
    public override string AggregateType => "MaterialRequirement";
    public override Guid AggregateId => MaterialRequirementId;
}

public record MaterialRequirementRejected(Guid MaterialRequirementId, Guid ProjectId, string Reason) : IntegrationEvent
{
    public override string EventType => "MaterialRequirementRejected";
    public override string AggregateType => "MaterialRequirement";
    public override Guid AggregateId => MaterialRequirementId;
}

public record PurchaseOrderStatusChanged(Guid PurchaseOrderId, string Status) : IntegrationEvent
{
    public override string EventType => "PurchaseOrderStatusChanged";
    public override string AggregateType => "PurchaseOrder";
    public override Guid AggregateId => PurchaseOrderId;
}
