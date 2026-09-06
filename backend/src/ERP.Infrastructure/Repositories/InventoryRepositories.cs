using ERP.Application.Abstractions;
using ERP.Domain.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Shared.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public sealed class ItemRepository : IItemRepository
{
    private readonly ErpDbContext _db;
    public ItemRepository(ErpDbContext db) => _db = db;

    public async Task<Item?> GetByIdAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default)
    {
        var q = _db.Items.Where(i => i.OrganizationId == organizationId && i.Id == id);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<Item>> ListAsync(Guid organizationId, ItemFilter filter, CancellationToken ct = default)
    {
        var q = _db.Items.AsNoTracking().Where(i => i.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(filter.ItemType)) q = q.Where(i => i.ItemType == filter.ItemType);
        if (filter.CategoryId.HasValue) q = q.Where(i => i.CategoryId == filter.CategoryId);
        if (filter.IsActive.HasValue) q = q.Where(i => i.IsActive == filter.IsActive);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            q = q.Where(i => EF.Functions.Like(i.Name, $"%{term}%") || EF.Functions.Like(i.Code, $"%{term}%"));
        }
        if (filter.LowStock == true)
        {
            q = q.Where(i => i.ReorderLevel != null &&
                _db.StockLevels.Where(s => s.ItemId == i.Id).Sum(s => (decimal?)s.QtyOnHand).GetValueOrDefault() <= i.ReorderLevel);
        }

        var total = await q.LongCountAsync(ct);

        q = filter.Sort?.ToLowerInvariant() switch
        {
            "name" => q.OrderBy(i => i.Name),
            "-name" => q.OrderByDescending(i => i.Name),
            "code" => q.OrderBy(i => i.Code),
            "-code" => q.OrderByDescending(i => i.Code),
            _ => q.OrderBy(i => i.Name)
        };

        var items = await q.Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<Item> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<bool> ExistsAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.Items.AnyAsync(i => i.OrganizationId == organizationId && i.Id == id, ct);

    public Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default) =>
        _db.Items.IgnoreQueryFilters()
            .AnyAsync(i => i.OrganizationId == organizationId && i.Code == code && (excludeId == null || i.Id != excludeId), ct);

    public async Task AddAsync(Item item, CancellationToken ct = default) => await _db.Items.AddAsync(item, ct);
}

public sealed class ItemCategoryRepository : IItemCategoryRepository
{
    private readonly ErpDbContext _db;
    public ItemCategoryRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<ItemCategory>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.ItemCategories.AsNoTracking().Where(c => c.OrganizationId == organizationId)
            .OrderBy(c => c.Name).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.ItemCategories.AnyAsync(c => c.OrganizationId == organizationId && c.Id == id, ct);

    public Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default) =>
        _db.ItemCategories.AnyAsync(c => c.OrganizationId == organizationId && c.Code == code && (excludeId == null || c.Id != excludeId), ct);

    public async Task AddAsync(ItemCategory category, CancellationToken ct = default) => await _db.ItemCategories.AddAsync(category, ct);
}

public sealed class UnitOfMeasureRepository : IUnitOfMeasureRepository
{
    private readonly ErpDbContext _db;
    public UnitOfMeasureRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<UnitOfMeasure>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.UnitsOfMeasure.AsNoTracking().Where(u => u.OrganizationId == organizationId)
            .OrderBy(u => u.Code).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.UnitsOfMeasure.AnyAsync(u => u.OrganizationId == organizationId && u.Id == id, ct);
}

public sealed class WarehouseRepository : IWarehouseRepository
{
    private readonly ErpDbContext _db;
    public WarehouseRepository(ErpDbContext db) => _db = db;

    public Task<Warehouse?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default)
    {
        var q = track ? _db.Warehouses : _db.Warehouses.AsNoTracking();
        return q.FirstOrDefaultAsync(w => w.OrganizationId == organizationId && w.Id == id, ct);
    }

    public async Task<IReadOnlyList<Warehouse>> ListAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.Warehouses.AsNoTracking().Where(w => w.OrganizationId == organizationId)
            .OrderBy(w => w.Name).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.Warehouses.AnyAsync(w => w.OrganizationId == organizationId && w.Id == id, ct);

    public Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default) =>
        _db.Warehouses.AnyAsync(w => w.OrganizationId == organizationId && w.Code == code && (excludeId == null || w.Id != excludeId), ct);

    public async Task AddAsync(Warehouse warehouse, CancellationToken ct = default) => await _db.Warehouses.AddAsync(warehouse, ct);
}

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly ErpDbContext _db;
    public SupplierRepository(ErpDbContext db) => _db = db;

    public Task<Supplier?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default)
    {
        var q = track ? _db.Suppliers : _db.Suppliers.AsNoTracking();
        return q.FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.Id == id, ct);
    }

    public async Task<PagedResult<Supplier>> ListAsync(Guid organizationId, SupplierFilter filter, CancellationToken ct = default)
    {
        var q = _db.Suppliers.AsNoTracking().Where(s => s.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(s => s.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            q = q.Where(s => EF.Functions.Like(s.Name, $"%{term}%") || EF.Functions.Like(s.Code, $"%{term}%"));
        }
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderBy(s => s.Name).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<Supplier> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<bool> ExistsAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.Suppliers.AnyAsync(s => s.OrganizationId == organizationId && s.Id == id, ct);

    public Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default) =>
        _db.Suppliers.AnyAsync(s => s.OrganizationId == organizationId && s.Code == code && (excludeId == null || s.Id != excludeId), ct);

    public async Task AddAsync(Supplier supplier, CancellationToken ct = default) => await _db.Suppliers.AddAsync(supplier, ct);
}

public sealed class BatchRepository : IBatchRepository
{
    private readonly ErpDbContext _db;
    public BatchRepository(ErpDbContext db) => _db = db;

    public Task<Batch?> GetByNoAsync(Guid organizationId, Guid itemId, string batchNo, bool track, CancellationToken ct = default)
    {
        var q = track ? _db.Batches : _db.Batches.AsNoTracking();
        return q.FirstOrDefaultAsync(b => b.OrganizationId == organizationId && b.ItemId == itemId && b.BatchNo == batchNo, ct);
    }

    public Task<Batch?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.Batches.AsNoTracking().FirstOrDefaultAsync(b => b.OrganizationId == organizationId && b.Id == id, ct);

    public async Task AddAsync(Batch batch, CancellationToken ct = default) => await _db.Batches.AddAsync(batch, ct);
}

public sealed class StockLevelRepository : IStockLevelRepository
{
    private readonly ErpDbContext _db;
    public StockLevelRepository(ErpDbContext db) => _db = db;

    public Task<StockLevel?> GetAsync(Guid organizationId, Guid itemId, Guid warehouseId, Guid? batchId, bool track, CancellationToken ct = default)
    {
        var q = track ? _db.StockLevels : _db.StockLevels.AsNoTracking();
        return q.FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.ItemId == itemId
            && s.WarehouseId == warehouseId && s.BatchId == batchId, ct);
    }

    public async Task<PagedResult<StockLevel>> ListAsync(Guid organizationId, StockLevelFilter filter, CancellationToken ct = default)
    {
        var q = _db.StockLevels.AsNoTracking().Where(s => s.OrganizationId == organizationId);
        if (filter.ItemId.HasValue) q = q.Where(s => s.ItemId == filter.ItemId);
        if (filter.WarehouseId.HasValue) q = q.Where(s => s.WarehouseId == filter.WarehouseId);
        if (filter.BelowReorder == true)
        {
            q = q.Where(s => _db.Items.Any(i => i.Id == s.ItemId && i.ReorderLevel != null && s.QtyOnHand <= i.ReorderLevel));
        }
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(s => s.QtyOnHand).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<StockLevel> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public async Task AddAsync(StockLevel level, CancellationToken ct = default) => await _db.StockLevels.AddAsync(level, ct);
}

public sealed class StockMovementRepository : IStockMovementRepository
{
    private readonly ErpDbContext _db;
    public StockMovementRepository(ErpDbContext db) => _db = db;

    public async Task<PagedResult<StockMovement>> ListAsync(Guid organizationId, StockMovementFilter filter, CancellationToken ct = default)
    {
        var q = _db.StockMovements.AsNoTracking().Where(m => m.OrganizationId == organizationId);
        if (filter.ItemId.HasValue) q = q.Where(m => m.ItemId == filter.ItemId);
        if (filter.WarehouseId.HasValue) q = q.Where(m => m.WarehouseId == filter.WarehouseId);
        if (filter.ProjectId.HasValue) q = q.Where(m => m.ProjectId == filter.ProjectId);
        if (!string.IsNullOrWhiteSpace(filter.MovementType)) q = q.Where(m => m.MovementType == filter.MovementType);
        if (filter.DateFrom.HasValue) q = q.Where(m => m.OccurredAt >= filter.DateFrom.Value.ToDateTime(TimeOnly.MinValue));
        if (filter.DateTo.HasValue) q = q.Where(m => m.OccurredAt <= filter.DateTo.Value.ToDateTime(TimeOnly.MaxValue));

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(m => m.OccurredAt).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<StockMovement> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public async Task AddAsync(StockMovement movement, CancellationToken ct = default) => await _db.StockMovements.AddAsync(movement, ct);
}

public sealed class GoodsReceiptRepository : IGoodsReceiptRepository
{
    private readonly ErpDbContext _db;
    public GoodsReceiptRepository(ErpDbContext db) => _db = db;

    public Task<GoodsReceipt?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.GoodsReceipts.AsNoTracking().Include(g => g.Lines)
            .FirstOrDefaultAsync(g => g.OrganizationId == organizationId && g.Id == id, ct);

    public async Task<PagedResult<GoodsReceipt>> ListAsync(Guid organizationId, InventoryDocFilter filter, CancellationToken ct = default)
    {
        var q = _db.GoodsReceipts.AsNoTracking().Where(g => g.OrganizationId == organizationId);
        if (filter.WarehouseId.HasValue) q = q.Where(g => g.WarehouseId == filter.WarehouseId);
        if (filter.SupplierId.HasValue) q = q.Where(g => g.SupplierId == filter.SupplierId);
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(g => g.CreatedAt).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<GoodsReceipt> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default) =>
        _db.GoodsReceipts.AnyAsync(g => g.OrganizationId == organizationId && g.GrnNumber == number, ct);

    public async Task AddAsync(GoodsReceipt receipt, CancellationToken ct = default) => await _db.GoodsReceipts.AddAsync(receipt, ct);
}

public sealed class MaterialIssueRepository : IMaterialIssueRepository
{
    private readonly ErpDbContext _db;
    public MaterialIssueRepository(ErpDbContext db) => _db = db;

    public Task<MaterialIssue?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.MaterialIssues.AsNoTracking().Include(m => m.Lines)
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.Id == id, ct);

    public async Task<PagedResult<MaterialIssue>> ListAsync(Guid organizationId, InventoryDocFilter filter, CancellationToken ct = default)
    {
        var q = _db.MaterialIssues.AsNoTracking().Where(m => m.OrganizationId == organizationId);
        if (filter.WarehouseId.HasValue) q = q.Where(m => m.WarehouseId == filter.WarehouseId);
        if (filter.ProjectId.HasValue) q = q.Where(m => m.ProjectId == filter.ProjectId);
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(m => m.CreatedAt).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<MaterialIssue> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default) =>
        _db.MaterialIssues.AnyAsync(m => m.OrganizationId == organizationId && m.IssueNumber == number, ct);

    public async Task AddAsync(MaterialIssue issue, CancellationToken ct = default) => await _db.MaterialIssues.AddAsync(issue, ct);
}

public sealed class StockTransferRepository : IStockTransferRepository
{
    private readonly ErpDbContext _db;
    public StockTransferRepository(ErpDbContext db) => _db = db;

    public Task<StockTransfer?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.StockTransfers.AsNoTracking().Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.OrganizationId == organizationId && t.Id == id, ct);

    public async Task<PagedResult<StockTransfer>> ListAsync(Guid organizationId, InventoryDocFilter filter, CancellationToken ct = default)
    {
        var q = _db.StockTransfers.AsNoTracking().Where(t => t.OrganizationId == organizationId);
        if (filter.WarehouseId.HasValue) q = q.Where(t => t.FromWarehouseId == filter.WarehouseId || t.ToWarehouseId == filter.WarehouseId);
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(t => t.CreatedAt).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<StockTransfer> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default) =>
        _db.StockTransfers.AnyAsync(t => t.OrganizationId == organizationId && t.TransferNumber == number, ct);

    public async Task AddAsync(StockTransfer transfer, CancellationToken ct = default) => await _db.StockTransfers.AddAsync(transfer, ct);
}

public sealed class StockAdjustmentRepository : IStockAdjustmentRepository
{
    private readonly ErpDbContext _db;
    public StockAdjustmentRepository(ErpDbContext db) => _db = db;

    public Task<StockAdjustment?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default) =>
        _db.StockAdjustments.AsNoTracking().Include(a => a.Lines)
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId && a.Id == id, ct);

    public async Task<PagedResult<StockAdjustment>> ListAsync(Guid organizationId, InventoryDocFilter filter, CancellationToken ct = default)
    {
        var q = _db.StockAdjustments.AsNoTracking().Where(a => a.OrganizationId == organizationId);
        if (filter.WarehouseId.HasValue) q = q.Where(a => a.WarehouseId == filter.WarehouseId);
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(a => a.CreatedAt).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<StockAdjustment> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default) =>
        _db.StockAdjustments.AnyAsync(a => a.OrganizationId == organizationId && a.AdjustmentNumber == number, ct);

    public async Task AddAsync(StockAdjustment adjustment, CancellationToken ct = default) => await _db.StockAdjustments.AddAsync(adjustment, ct);
}

public sealed class BomRepository : IBomRepository
{
    private readonly ErpDbContext _db;
    public BomRepository(ErpDbContext db) => _db = db;

    public Task<BillOfMaterials?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default)
    {
        var q = track ? _db.BillsOfMaterials.Include(b => b.Lines) : _db.BillsOfMaterials.AsNoTracking().Include(b => b.Lines);
        return q.FirstOrDefaultAsync(b => b.OrganizationId == organizationId && b.Id == id, ct);
    }

    public async Task<PagedResult<BillOfMaterials>> ListAsync(Guid organizationId, BomFilter filter, CancellationToken ct = default)
    {
        var q = _db.BillsOfMaterials.AsNoTracking().Include(b => b.Lines).Where(b => b.OrganizationId == organizationId);
        if (filter.OutputItemId.HasValue) q = q.Where(b => b.OutputItemId == filter.OutputItemId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            q = q.Where(b => EF.Functions.Like(b.Code, $"%{term}%"));
        }
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(b => b.CreatedAt).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<BillOfMaterials> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default) =>
        _db.BillsOfMaterials.AnyAsync(b => b.OrganizationId == organizationId && b.Code == code && (excludeId == null || b.Id != excludeId), ct);

    public async Task AddAsync(BillOfMaterials bom, CancellationToken ct = default) => await _db.BillsOfMaterials.AddAsync(bom, ct);
}

public sealed class WorkOrderRepository : IWorkOrderRepository
{
    private readonly ErpDbContext _db;
    public WorkOrderRepository(ErpDbContext db) => _db = db;

    public Task<WorkOrder?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default)
    {
        var q = track ? _db.WorkOrders.Include(w => w.Components) : _db.WorkOrders.AsNoTracking().Include(w => w.Components);
        return q.FirstOrDefaultAsync(w => w.OrganizationId == organizationId && w.Id == id, ct);
    }

    public async Task<PagedResult<WorkOrder>> ListAsync(Guid organizationId, WorkOrderFilter filter, CancellationToken ct = default)
    {
        var q = _db.WorkOrders.AsNoTracking().Where(w => w.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(w => w.Status == filter.Status);
        if (filter.ProjectId.HasValue) q = q.Where(w => w.ProjectId == filter.ProjectId);
        if (filter.WarehouseId.HasValue) q = q.Where(w => w.WarehouseId == filter.WarehouseId);
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(w => w.CreatedAt).Skip(filter.Skip).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<WorkOrder> { Items = items, TotalItems = total, Page = filter.Page, PageSize = filter.PageSize };
    }

    public Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default) =>
        _db.WorkOrders.AnyAsync(w => w.OrganizationId == organizationId && w.WoNumber == number, ct);

    public async Task AddAsync(WorkOrder workOrder, CancellationToken ct = default) => await _db.WorkOrders.AddAsync(workOrder, ct);
}
