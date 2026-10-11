using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public sealed class FabricationRepository : IFabricationRepository
{
    private readonly ErpDbContext _db;
    public FabricationRepository(ErpDbContext db) => _db = db;

    public async Task<HashSet<Guid>> ManufacturedItemIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct = default) =>
        (await _db.Items.AsNoTracking()
            .Where(i => i.OrganizationId == organizationId && i.IsManufactured && itemIds.Contains(i.Id))
            .Select(i => i.Id).ToListAsync(ct)).ToHashSet();

    public async Task<Dictionary<Guid, decimal>> AvailableStockAsync(Guid organizationId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct = default) =>
        await _db.StockLevels.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && itemIds.Contains(s.ItemId))
            .GroupBy(s => s.ItemId)
            .Select(g => new { g.Key, Qty = g.Sum(s => s.QtyOnHand - s.QtyReserved) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

    public async Task<Guid?> ProductionWarehouseIdAsync(Guid organizationId, Guid? preferredWarehouseId, CancellationToken ct = default)
    {
        var production = await _db.Warehouses.AsNoTracking()
            .Where(w => w.OrganizationId == organizationId && w.IsActive && w.WarehouseType == WarehouseTypes.ProductionStore)
            .OrderBy(w => w.Name).Select(w => (Guid?)w.Id).FirstOrDefaultAsync(ct);
        if (production.HasValue) return production;
        if (preferredWarehouseId.HasValue) return preferredWarehouseId;
        return await _db.Warehouses.AsNoTracking()
            .Where(w => w.OrganizationId == organizationId && w.IsActive)
            .OrderBy(w => w.Name).Select(w => (Guid?)w.Id).FirstOrDefaultAsync(ct);
    }

    public async Task<HashSet<Guid>> FabricatedItemsForRequirementAsync(Guid organizationId, Guid requirementId, CancellationToken ct = default) =>
        (await _db.WorkOrders.AsNoTracking()
            .Where(w => w.OrganizationId == organizationId && w.MaterialRequirementId == requirementId && w.Status != WorkOrderStatuses.Cancelled)
            .Select(w => w.OutputItemId).ToListAsync(ct)).ToHashSet();
}
