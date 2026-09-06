using ERP.Domain.Entities;
using ERP.Shared.Pagination;

namespace ERP.Application.Abstractions;

public sealed class ItemFilter : PageRequest
{
    public string? ItemType { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Search { get; set; }
    public bool? LowStock { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class SupplierFilter : PageRequest
{
    public string? Search { get; set; }
    public string? Status { get; set; }
}

public sealed class StockLevelFilter : PageRequest
{
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public bool? BelowReorder { get; set; }
}

public sealed class StockMovementFilter : PageRequest
{
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? MovementType { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}

public sealed class InventoryDocFilter : PageRequest
{
    public Guid? WarehouseId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ProjectId { get; set; }
}

public interface IItemRepository
{
    Task<Item?> GetByIdAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default);
    Task<PagedResult<Item>> ListAsync(Guid organizationId, ItemFilter filter, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Item item, CancellationToken ct = default);
}

public interface IItemCategoryRepository
{
    Task<IReadOnlyList<ItemCategory>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(ItemCategory category, CancellationToken ct = default);
}

public interface IUnitOfMeasureRepository
{
    Task<IReadOnlyList<UnitOfMeasure>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid organizationId, Guid id, CancellationToken ct = default);
}

public interface IWarehouseRepository
{
    Task<Warehouse?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default);
    Task<IReadOnlyList<Warehouse>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Warehouse warehouse, CancellationToken ct = default);
}

public interface ISupplierRepository
{
    Task<Supplier?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default);
    Task<PagedResult<Supplier>> ListAsync(Guid organizationId, SupplierFilter filter, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(Guid organizationId, string code, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Supplier supplier, CancellationToken ct = default);
}

public interface IBatchRepository
{
    Task<Batch?> GetByNoAsync(Guid organizationId, Guid itemId, string batchNo, bool track, CancellationToken ct = default);
    Task<Batch?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task AddAsync(Batch batch, CancellationToken ct = default);
}

public interface IStockLevelRepository
{
    Task<StockLevel?> GetAsync(Guid organizationId, Guid itemId, Guid warehouseId, Guid? batchId, bool track, CancellationToken ct = default);
    Task<PagedResult<StockLevel>> ListAsync(Guid organizationId, StockLevelFilter filter, CancellationToken ct = default);
    Task AddAsync(StockLevel level, CancellationToken ct = default);
}

public interface IStockMovementRepository
{
    Task<PagedResult<StockMovement>> ListAsync(Guid organizationId, StockMovementFilter filter, CancellationToken ct = default);
    Task AddAsync(StockMovement movement, CancellationToken ct = default);
}

public interface IGoodsReceiptRepository
{
    Task<GoodsReceipt?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<PagedResult<GoodsReceipt>> ListAsync(Guid organizationId, InventoryDocFilter filter, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default);
    Task AddAsync(GoodsReceipt receipt, CancellationToken ct = default);
}

public interface IMaterialIssueRepository
{
    Task<MaterialIssue?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<PagedResult<MaterialIssue>> ListAsync(Guid organizationId, InventoryDocFilter filter, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default);
    Task AddAsync(MaterialIssue issue, CancellationToken ct = default);
}

public interface IStockTransferRepository
{
    Task<StockTransfer?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<PagedResult<StockTransfer>> ListAsync(Guid organizationId, InventoryDocFilter filter, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default);
    Task AddAsync(StockTransfer transfer, CancellationToken ct = default);
}

public interface IStockAdjustmentRepository
{
    Task<StockAdjustment?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    Task<PagedResult<StockAdjustment>> ListAsync(Guid organizationId, InventoryDocFilter filter, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default);
    Task AddAsync(StockAdjustment adjustment, CancellationToken ct = default);
}
