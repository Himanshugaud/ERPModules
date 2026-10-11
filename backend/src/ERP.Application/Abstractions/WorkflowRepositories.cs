using ERP.Domain.Entities;

namespace ERP.Application.Abstractions;

public sealed record BlobDownload(Stream Content, string? ContentType);

public interface IBlobStorage
{
    string Container { get; }
    Task UploadAsync(string path, Stream content, string? contentType, CancellationToken ct = default);
    Task<BlobDownload?> OpenReadAsync(string path, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
}

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken ct = default);
    Task<Document?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> ListForEntityAsync(Guid organizationId, string entityType, Guid entityId, string? category, CancellationToken ct = default, bool track = false);
    Task<int> CountForEntityAsync(Guid organizationId, string entityType, Guid entityId, string category, CancellationToken ct = default);
}

public interface IProjectPlanRepository
{
    Task<ProjectPlan?> GetPlanAsync(Guid organizationId, Guid projectId, bool track, CancellationToken ct = default);
    Task AddPlanAsync(ProjectPlan plan, CancellationToken ct = default);
    Task<IReadOnlyList<PlanEntry>> ListEntriesAsync(Guid organizationId, Guid projectId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<PlanEntry?> GetEntryAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default);
    Task AddEntryAsync(PlanEntry entry, CancellationToken ct = default);
    void RemoveEntry(PlanEntry entry);
    Task<int> CountEntriesAsync(Guid organizationId, Guid projectId, CancellationToken ct = default);
}
