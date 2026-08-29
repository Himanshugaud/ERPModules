using ERP.Application.Abstractions;
using ERP.Domain.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Shared.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly ErpDbContext _db;
    public ProjectRepository(ErpDbContext db) => _db = db;

    public async Task<Project?> GetByIdAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default)
    {
        var query = _db.Projects.Where(p => p.OrganizationId == organizationId && p.Id == id);
        if (!track) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<Project>> ListAsync(Guid organizationId, ProjectFilter filter, CancellationToken ct = default)
    {
        var query = _db.Projects.AsNoTracking().Where(p => p.OrganizationId == organizationId);

        if (filter.StatusId.HasValue) query = query.Where(p => p.StatusId == filter.StatusId);
        if (filter.PriorityId.HasValue) query = query.Where(p => p.PriorityId == filter.PriorityId);
        if (filter.ManagerId.HasValue) query = query.Where(p => p.ManagerId == filter.ManagerId);
        if (filter.ClientId.HasValue) query = query.Where(p => p.ClientId == filter.ClientId);
        if (filter.StartDateFrom.HasValue) query = query.Where(p => p.StartDate >= filter.StartDateFrom);
        if (filter.StartDateTo.HasValue) query = query.Where(p => p.StartDate <= filter.StartDateTo);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{term}%") || EF.Functions.Like(p.Code, $"%{term}%"));
        }

        var total = await query.LongCountAsync(ct);

        query = filter.Sort?.ToLowerInvariant() switch
        {
            "name" => query.OrderBy(p => p.Name),
            "-name" => query.OrderByDescending(p => p.Name),
            "code" => query.OrderBy(p => p.Code),
            "-createdat" => query.OrderByDescending(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var items = await query.Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);

        return new PagedResult<Project>
        {
            Items = items,
            TotalItems = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default) =>
        _db.Projects.IgnoreQueryFilters()
            .AnyAsync(p => p.OrganizationId == organizationId && p.Code == code && (excludeId == null || p.Id != excludeId), ct);

    public async Task AddAsync(Project project, CancellationToken ct = default) =>
        await _db.Projects.AddAsync(project, ct);
}

public sealed class ClientRepository : IClientRepository
{
    private readonly ErpDbContext _db;
    public ClientRepository(ErpDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid organizationId, Guid clientId, CancellationToken ct = default) =>
        _db.Clients.AnyAsync(c => c.OrganizationId == organizationId && c.Id == clientId, ct);

    public async Task<IReadOnlyList<Client>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Clients.AsNoTracking().Where(c => c.OrganizationId == organizationId)
            .OrderBy(c => c.Name).ToListAsync(ct);
}

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly ErpDbContext _db;
    public OrganizationRepository(ErpDbContext db) => _db = db;

    public Task<Organization?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Code == code, ct);

    public Task<Organization?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
}

public sealed class ProjectStatusRepository : IProjectStatusRepository
{
    private readonly ErpDbContext _db;
    public ProjectStatusRepository(ErpDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid organizationId, Guid statusId, CancellationToken ct = default) =>
        _db.ProjectStatuses.AnyAsync(s => s.OrganizationId == organizationId && s.Id == statusId, ct);

    public async Task<IReadOnlyList<ProjectStatus>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.ProjectStatuses.AsNoTracking().Where(s => s.OrganizationId == organizationId)
            .OrderBy(s => s.DisplayOrder).ToListAsync(ct);
}

public sealed class ProjectPriorityRepository : IProjectPriorityRepository
{
    private readonly ErpDbContext _db;
    public ProjectPriorityRepository(ErpDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid organizationId, Guid priorityId, CancellationToken ct = default) =>
        _db.ProjectPriorities.AnyAsync(p => p.OrganizationId == organizationId && p.Id == priorityId, ct);

    public async Task<IReadOnlyList<ProjectPriority>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.ProjectPriorities.AsNoTracking().Where(p => p.OrganizationId == organizationId)
            .OrderBy(p => p.DisplayOrder).ToListAsync(ct);
}
