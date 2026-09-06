using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;

namespace ERP.Application.Inventory;

/// <summary>
/// Centralised stock posting. Maintains the append-only StockMovements ledger and the
/// StockLevels projection (weighted-average valuation). Levels are tracked at the
/// (Item, Warehouse) grain; batch is recorded on movements for traceability.
/// Callers own the transaction/SaveChanges (via IUnitOfWork).
/// </summary>
public interface IStockLedger
{
    Task<StockLevel> ReceiveAsync(Guid itemId, Guid warehouseId, decimal qty, decimal unitCost,
        string movementType, string refDocType, Guid refDocId, Guid? batchId, Guid? projectId, CancellationToken ct = default);

    Task<StockLevel> IssueAsync(Guid itemId, Guid warehouseId, decimal qty,
        string movementType, string refDocType, Guid refDocId, Guid? batchId, Guid? projectId, CancellationToken ct = default);
}

public sealed class StockLedger : IStockLedger
{
    private readonly IStockLevelRepository _levels;
    private readonly IStockMovementRepository _movements;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public StockLedger(IStockLevelRepository levels, IStockMovementRepository movements, ITenantContext tenant, IClock clock)
    {
        _levels = levels;
        _movements = movements;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<StockLevel> ReceiveAsync(Guid itemId, Guid warehouseId, decimal qty, decimal unitCost,
        string movementType, string refDocType, Guid refDocId, Guid? batchId, Guid? projectId, CancellationToken ct = default)
    {
        if (qty <= 0) throw new ConflictException("Receipt quantity must be greater than zero.");
        var orgId = _tenant.OrganizationId;

        var level = await _levels.GetAsync(orgId, itemId, warehouseId, null, track: true, ct);
        if (level is null)
        {
            level = new StockLevel
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                ItemId = itemId,
                WarehouseId = warehouseId,
                QtyOnHand = 0,
                AvgUnitCost = 0
            };
            await _levels.AddAsync(level, ct);
        }

        var newQty = level.QtyOnHand + qty;
        level.AvgUnitCost = newQty > 0
            ? ((level.QtyOnHand * level.AvgUnitCost) + (qty * unitCost)) / newQty
            : unitCost;
        level.QtyOnHand = newQty;
        level.UpdatedAt = _clock.UtcNow;

        await AddMovementAsync(orgId, itemId, warehouseId, batchId, movementType, MovementDirection.In,
            qty, unitCost, refDocType, refDocId, projectId, ct);

        return level;
    }

    public async Task<StockLevel> IssueAsync(Guid itemId, Guid warehouseId, decimal qty,
        string movementType, string refDocType, Guid refDocId, Guid? batchId, Guid? projectId, CancellationToken ct = default)
    {
        if (qty <= 0) throw new ConflictException("Issue quantity must be greater than zero.");
        var orgId = _tenant.OrganizationId;

        var level = await _levels.GetAsync(orgId, itemId, warehouseId, null, track: true, ct);
        var available = level is null ? 0 : level.QtyOnHand - level.QtyReserved;
        if (level is null || available < qty)
            throw new ConflictException($"Insufficient available stock for item {itemId} in warehouse {warehouseId}. Available: {available}, requested: {qty}.");

        var unitCost = level.AvgUnitCost;
        level.QtyOnHand -= qty;
        level.UpdatedAt = _clock.UtcNow;

        await AddMovementAsync(orgId, itemId, warehouseId, batchId, movementType, MovementDirection.Out,
            qty, unitCost, refDocType, refDocId, projectId, ct);

        return level;
    }

    private async Task AddMovementAsync(Guid orgId, Guid itemId, Guid warehouseId, Guid? batchId,
        string movementType, string direction, decimal qty, decimal unitCost, string refDocType, Guid refDocId,
        Guid? projectId, CancellationToken ct)
    {
        await _movements.AddAsync(new StockMovement
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ItemId = itemId,
            WarehouseId = warehouseId,
            BatchId = batchId,
            MovementType = movementType,
            Direction = direction,
            Qty = qty,
            UnitCost = unitCost,
            TotalCost = qty * unitCost,
            RefDocType = refDocType,
            RefDocId = refDocId,
            ProjectId = projectId,
            OccurredAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        }, ct);
    }
}
