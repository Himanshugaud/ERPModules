namespace ERP.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    Guid OrganizationId { get; }
    string? OrganizationName { get; }
    string? Email { get; }
    string? DisplayName { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool HasPermission(string permission);
}

public interface ITenantContext
{
    Guid OrganizationId { get; }
    Guid UserId { get; }
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}

public interface IAuditWriter
{
    void Add(string entityType, Guid? entityId, string action, object? oldValues = null, object? newValues = null);
}

public interface IOutboxWriter
{
    void Enqueue(ERP.Domain.Events.IntegrationEvent @event);
}

public interface IEventPublisher
{
    Task PublishAsync(string eventType, string payloadJson, IDictionary<string, string> properties, CancellationToken ct = default);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) Generate(
        Guid userId,
        Guid organizationId,
        string? organizationName,
        string? email,
        string? name,
        IEnumerable<string> roles,
        IEnumerable<string> permissions);

    System.Security.Claims.ClaimsPrincipal? Validate(string token);
}
