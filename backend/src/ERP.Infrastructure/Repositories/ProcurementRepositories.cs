using ERP.Application.Abstractions;
using ERP.Domain.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Shared.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public sealed class MaterialRequirementRepository : IMaterialRequirementRepository
{
    private readonly ErpDbContext _db;
    public MaterialRequirementRepository(ErpDbContext db) => _db = db;

    public Task<MaterialRequirement?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default)
    {
        var q = track ? _db.MaterialRequirements.Include(r => r.Lines) : _db.MaterialRequirements.AsNoTracking().Include(r => r.Lines);
        return q.FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.Id == id, ct);
    }

    public async Task<PagedResult<MaterialRequirement>> ListAsync(Guid organizationId, MaterialRequirementFilter filter, CancellationToken ct = default)
    {
        var q = _db.MaterialRequirements.AsNoTracking().Include(r => r.Lines).Where(r => r.OrganizationId == organizationId);
        if (filter.ProjectId.HasValue) q = q.Where(r => r.ProjectId == filter.ProjectId);
        if (filter.WarehouseId.HasValue) q = q.Where(r => r.WarehouseId == filter.WarehouseId);
        if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(r => r.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Priority)) q = q.Where(r => r.Priority == filter.Priority);

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(r => r.CreatedAt).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<MaterialRequirement> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default) =>
        _db.MaterialRequirements.AnyAsync(r => r.OrganizationId == organizationId && r.ReqNumber == number, ct);

    public async Task AddAsync(MaterialRequirement requirement, CancellationToken ct = default) => await _db.MaterialRequirements.AddAsync(requirement, ct);
}

public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly ErpDbContext _db;
    public PurchaseOrderRepository(ErpDbContext db) => _db = db;

    public Task<PurchaseOrder?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default)
    {
        var q = track ? _db.PurchaseOrders.Include(o => o.Lines) : _db.PurchaseOrders.AsNoTracking().Include(o => o.Lines);
        return q.FirstOrDefaultAsync(o => o.OrganizationId == organizationId && o.Id == id, ct);
    }

    public async Task<PagedResult<PurchaseOrder>> ListAsync(Guid organizationId, PurchaseOrderFilter filter, CancellationToken ct = default)
    {
        var q = _db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).Where(o => o.OrganizationId == organizationId);
        if (filter.SupplierId.HasValue) q = q.Where(o => o.SupplierId == filter.SupplierId);
        if (filter.ProjectId.HasValue) q = q.Where(o => o.ProjectId == filter.ProjectId);
        if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(o => o.Status == filter.Status);

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(o => o.CreatedAt).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<PurchaseOrder> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default) =>
        _db.PurchaseOrders.AnyAsync(o => o.OrganizationId == organizationId && o.PoNumber == number, ct);

    public async Task AddAsync(PurchaseOrder order, CancellationToken ct = default) => await _db.PurchaseOrders.AddAsync(order, ct);
}
