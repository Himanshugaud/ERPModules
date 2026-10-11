using ERP.Application.Abstractions;
using ERP.Domain.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public sealed class DocumentRepository : IDocumentRepository
{
    private readonly ErpDbContext _db;
    public DocumentRepository(ErpDbContext db) => _db = db;

    public async Task AddAsync(Document document, CancellationToken ct = default) => await _db.Documents.AddAsync(document, ct);

    public Task<Document?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default)
    {
        var q = track ? _db.Documents : _db.Documents.AsNoTracking();
        return q.FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.Id == id && d.Status == "ACTIVE", ct);
    }

    public async Task<IReadOnlyList<Document>> ListForEntityAsync(Guid organizationId, string entityType, Guid entityId, string? category, CancellationToken ct = default, bool track = false)
    {
        var q = (track ? _db.Documents : _db.Documents.AsNoTracking())
            .Where(d => d.OrganizationId == organizationId && d.EntityType == entityType && d.EntityId == entityId && d.Status == "ACTIVE");
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(d => d.Category == category);
        return await q.OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
    }

    public Task<int> CountForEntityAsync(Guid organizationId, string entityType, Guid entityId, string category, CancellationToken ct = default) =>
        _db.Documents.CountAsync(d => d.OrganizationId == organizationId && d.EntityType == entityType && d.EntityId == entityId
                                      && d.Category == category && d.Status == "ACTIVE", ct);
}

public sealed class ProjectPlanRepository : IProjectPlanRepository
{
    private readonly ErpDbContext _db;
    public ProjectPlanRepository(ErpDbContext db) => _db = db;

    public Task<ProjectPlan?> GetPlanAsync(Guid organizationId, Guid projectId, bool track, CancellationToken ct = default)
    {
        var q = track ? _db.ProjectPlans : _db.ProjectPlans.AsNoTracking();
        return q.FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.ProjectId == projectId, ct);
    }

    public async Task AddPlanAsync(ProjectPlan plan, CancellationToken ct = default) => await _db.ProjectPlans.AddAsync(plan, ct);

    public async Task<IReadOnlyList<PlanEntry>> ListEntriesAsync(Guid organizationId, Guid projectId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var q = _db.PlanEntries.AsNoTracking().Where(e => e.OrganizationId == organizationId && e.ProjectId == projectId);
        if (from.HasValue) q = q.Where(e => e.EndDate >= from.Value);
        if (to.HasValue) q = q.Where(e => e.StartDate <= to.Value);
        return await q.OrderBy(e => e.StartDate).ThenBy(e => e.CreatedAt).ToListAsync(ct);
    }

    public Task<PlanEntry?> GetEntryAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default)
    {
        var q = track ? _db.PlanEntries : _db.PlanEntries.AsNoTracking();
        return q.FirstOrDefaultAsync(e => e.OrganizationId == organizationId && e.Id == id, ct);
    }

    public async Task AddEntryAsync(PlanEntry entry, CancellationToken ct = default) => await _db.PlanEntries.AddAsync(entry, ct);

    public void RemoveEntry(PlanEntry entry) => _db.PlanEntries.Remove(entry);

    public Task<int> CountEntriesAsync(Guid organizationId, Guid projectId, CancellationToken ct = default) =>
        _db.PlanEntries.CountAsync(e => e.OrganizationId == organizationId && e.ProjectId == projectId, ct);
}
