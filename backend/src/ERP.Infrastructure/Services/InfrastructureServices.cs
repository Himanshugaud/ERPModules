using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Domain.Entities;
using ERP.Domain.Events;
using ERP.Infrastructure.Persistence;
using ERP.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class TenantContext : ITenantContext
{
    public Guid OrganizationId { get; }
    public Guid UserId { get; }

    public TenantContext(ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedException();
        OrganizationId = currentUser.OrganizationId;
        UserId = currentUser.UserId;
    }
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ErpDbContext _db;
    public UnitOfWork(ErpDbContext db) => _db = db;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException();
        }
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await action(ct);
            await tx.CommitAsync(ct);
        });
    }
}

public sealed class AuditWriter : IAuditWriter
{
    private readonly ErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public AuditWriter(ErpDbContext db, ITenantContext tenant, IClock clock)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
    }

    public void Add(string entityType, Guid? entityId, string action, object? oldValues = null, object? newValues = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenant.OrganizationId,
            UserId = _tenant.UserId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            CreatedAt = _clock.UtcNow
        });
    }
}

public sealed class OutboxWriter : IOutboxWriter
{
    private readonly ErpDbContext _db;
    private readonly IClock _clock;

    public OutboxWriter(ErpDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public void Enqueue(IntegrationEvent @event)
    {
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = @event.EventId,
            EventType = @event.EventType,
            AggregateType = @event.AggregateType,
            AggregateId = @event.AggregateId,
            PayloadJson = JsonSerializer.Serialize(@event, @event.GetType()),
            CreatedAt = _clock.UtcNow
        });
    }
}
