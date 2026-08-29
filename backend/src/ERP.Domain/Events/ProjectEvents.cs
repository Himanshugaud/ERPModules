namespace ERP.Domain.Events;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public abstract string EventType { get; }
    public abstract string AggregateType { get; }
    public abstract Guid AggregateId { get; }
    public Guid OrganizationId { get; init; }
}

public record ProjectCreated(Guid ProjectId, string Code, string Name) : IntegrationEvent
{
    public override string EventType => "ProjectCreated";
    public override string AggregateType => "Project";
    public override Guid AggregateId => ProjectId;
}

public record ProjectUpdated(Guid ProjectId) : IntegrationEvent
{
    public override string EventType => "ProjectUpdated";
    public override string AggregateType => "Project";
    public override Guid AggregateId => ProjectId;
}

public record ProjectArchived(Guid ProjectId) : IntegrationEvent
{
    public override string EventType => "ProjectArchived";
    public override string AggregateType => "Project";
    public override Guid AggregateId => ProjectId;
}
