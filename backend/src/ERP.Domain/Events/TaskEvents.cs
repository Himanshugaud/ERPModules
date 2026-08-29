namespace ERP.Domain.Events;

public record TaskCreated(Guid ProjectId, Guid TaskId, string Title) : IntegrationEvent
{
    public override string EventType => "TaskCreated";
    public override string AggregateType => "Task";
    public override Guid AggregateId => TaskId;
}

public record TaskUpdated(Guid ProjectId, Guid TaskId) : IntegrationEvent
{
    public override string EventType => "TaskUpdated";
    public override string AggregateType => "Task";
    public override Guid AggregateId => TaskId;
}

public record TaskAssigned(Guid ProjectId, Guid TaskId, Guid? AssigneeId) : IntegrationEvent
{
    public override string EventType => "TaskAssigned";
    public override string AggregateType => "Task";
    public override Guid AggregateId => TaskId;
}

public record TaskStatusChanged(Guid ProjectId, Guid TaskId, Guid? StatusId) : IntegrationEvent
{
    public override string EventType => "TaskStatusChanged";
    public override string AggregateType => "Task";
    public override Guid AggregateId => TaskId;
}

public record TaskCompleted(Guid ProjectId, Guid TaskId) : IntegrationEvent
{
    public override string EventType => "TaskCompleted";
    public override string AggregateType => "Task";
    public override Guid AggregateId => TaskId;
}
