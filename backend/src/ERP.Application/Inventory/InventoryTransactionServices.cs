using ERP.Application.Abstractions;
using ERP.Application.Projects;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Domain.Events;
using ERP.Shared.Exceptions;
using ERP.Shared.Pagination;

namespace ERP.Application.Inventory;

public interface IGoodsReceiptService
{
    Task<InventoryDocumentResponse> CreateAsync(CreateGoodsReceiptRequest request, CancellationToken ct = default);
    Task<PagedResult<InventoryDocumentResponse>> ListAsync(InventoryDocFilter filter, CancellationToken ct = default);
    Task<InventoryDocumentResponse> GetAsync(Guid id, CancellationToken ct = default);
}

public sealed class GoodsReceiptService : IGoodsReceiptService
{
    private readonly IGoodsReceiptRepository _receipts;
    private readonly IWarehouseRepository _warehouses;
    private readonly ISupplierRepository _suppliers;
    private readonly IItemRepository _items;
    private readonly IBatchRepository _batches;
    private readonly IPurchaseOrderRepository _purchaseOrders;
    private readonly IStockLedger _ledger;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IOutboxWriter _outbox;
    private readonly IClock _clock;

    private static readonly string[] ReceivablePoStatuses =
        { PurchaseOrderStatuses.Approved, PurchaseOrderStatuses.Ordered, PurchaseOrderStatuses.PartiallyReceived };

    public GoodsReceiptService(IGoodsReceiptRepository receipts, IWarehouseRepository warehouses, ISupplierRepository suppliers,
        IItemRepository items, IBatchRepository batches, IPurchaseOrderRepository purchaseOrders, IStockLedger ledger,
        ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit, IOutboxWriter outbox, IClock clock)
    {
        _receipts = receipts;
        _warehouses = warehouses;
        _suppliers = suppliers;
        _items = items;
        _batches = batches;
        _purchaseOrders = purchaseOrders;
        _ledger = ledger;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<InventoryDocumentResponse> CreateAsync(CreateGoodsReceiptRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (!await _warehouses.ExistsAsync(orgId, request.WarehouseId, ct))
            throw new ConflictException("Warehouse does not belong to the organization.");
        if (request.SupplierId.HasValue && !await _suppliers.ExistsAsync(orgId, request.SupplierId.Value, ct))
            throw new ConflictException("Supplier does not belong to the organization.");
        foreach (var line in request.Lines)
            if (!await _items.ExistsAsync(orgId, line.ItemId, ct))
                throw new ConflictException($"Item {line.ItemId} does not belong to the organization.");

        PurchaseOrder? po = null;
        if (request.PurchaseOrderId.HasValue)
        {
            po = await _purchaseOrders.GetAsync(orgId, request.PurchaseOrderId.Value, track: true, ct)
                 ?? throw new ConflictException("Purchase order does not belong to the organization.");
            if (!ReceivablePoStatuses.Contains(po.Status))
                throw new ConflictException($"Cannot receive against a purchase order in status '{po.Status}'.");
        }

        var number = string.IsNullOrWhiteSpace(request.GrnNumber)
            ? InventoryNumbers.Next("GRN")
            : request.GrnNumber.Trim();
        if (await _receipts.NumberExistsAsync(orgId, number, ct))
            throw new DuplicateEntityException($"A goods receipt with number '{number}' already exists.");

        var receipt = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            GrnNumber = number,
            SupplierId = request.SupplierId,
            PurchaseOrderId = request.PurchaseOrderId,
            PoReference = request.PoReference?.Trim(),
            WarehouseId = request.WarehouseId,
            ReceivedDate = request.ReceivedDate ?? DateOnly.FromDateTime(_clock.UtcNow),
            Status = "POSTED",
            Notes = request.Notes,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        decimal totalValue = 0;
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _receipts.AddAsync(receipt, token);

            foreach (var line in request.Lines)
            {
                Guid? batchId = await ResolveBatchAsync(orgId, line, request.SupplierId, token);
                receipt.Lines.Add(new GoodsReceiptLine
                {
                    Id = Guid.NewGuid(),
                    GoodsReceiptId = receipt.Id,
                    ItemId = line.ItemId,
                    Qty = line.Qty,
                    UomId = line.UomId,
                    UnitCost = line.UnitCost,
                    BatchId = batchId
                });

                await _ledger.ReceiveAsync(line.ItemId, request.WarehouseId, line.Qty, line.UnitCost,
                    MovementTypes.Receipt, RefDocTypes.GoodsReceipt, receipt.Id, batchId, null, token);

                if (po is not null)
                {
                    var poLine = po.Lines.FirstOrDefault(l => l.ItemId == line.ItemId && l.QtyReceived < l.Qty);
                    if (poLine is not null)
                        poLine.QtyReceived += line.Qty;
                }

                totalValue += line.Qty * line.UnitCost;
                _outbox.Enqueue(new StockReceived(line.ItemId, request.WarehouseId, line.Qty, line.UnitCost) { OrganizationId = orgId });
            }

            if (po is not null)
            {
                po.Status = po.Lines.All(l => l.QtyReceived >= l.Qty)
                    ? PurchaseOrderStatuses.Received
                    : PurchaseOrderStatuses.PartiallyReceived;
                po.UpdatedAt = _clock.UtcNow;
                po.UpdatedBy = _tenant.UserId;
                _audit.Add(EntityTypes.PurchaseOrder, po.Id, AuditActions.StatusChange, null, new { po.PoNumber, po.Status });
                _outbox.Enqueue(new PurchaseOrderStatusChanged(po.Id, po.Status) { OrganizationId = orgId });
            }

            _audit.Add(EntityTypes.GoodsReceipt, receipt.Id, AuditActions.Create, null, new { receipt.GrnNumber, LineCount = receipt.Lines.Count });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return ToDocument(receipt, totalValue);
    }

    public async Task<PagedResult<InventoryDocumentResponse>> ListAsync(InventoryDocFilter filter, CancellationToken ct = default)
    {
        var result = await _receipts.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<InventoryDocumentResponse>
        {
            Items = result.Items.Select(r => ToDocument(r, 0)).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<InventoryDocumentResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _receipts.GetAsync(_tenant.OrganizationId, id, ct) ?? throw NotFoundException.For("GoodsReceipt", id);
        var total = r.Lines.Sum(l => l.Qty * l.UnitCost);
        return ToDocument(r, total);
    }

    private async Task<Guid?> ResolveBatchAsync(Guid orgId, GoodsReceiptLineRequest line, Guid? supplierId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(line.BatchNo)) return null;
        var existing = await _batches.GetByNoAsync(orgId, line.ItemId, line.BatchNo.Trim(), track: false, ct);
        if (existing is not null) return existing.Id;

        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ItemId = line.ItemId,
            BatchNo = line.BatchNo.Trim(),
            ManufactureDate = line.ManufactureDate,
            ExpiryDate = line.ExpiryDate,
            SupplierId = supplierId,
            QcStatus = "RELEASED",
            CreatedAt = _clock.UtcNow
        };
        await _batches.AddAsync(batch, ct);
        return batch.Id;
    }

    private static InventoryDocumentResponse ToDocument(GoodsReceipt r, decimal totalValue) => new()
    {
        Id = r.Id,
        Number = r.GrnNumber,
        DocumentType = RefDocTypes.GoodsReceipt,
        WarehouseId = r.WarehouseId,
        SupplierId = r.SupplierId,
        Status = r.Status,
        DocumentDate = r.ReceivedDate,
        Reference = r.PoReference,
        LineCount = r.Lines.Count,
        TotalValue = totalValue,
        CreatedAt = r.CreatedAt
    };
}

public interface IMaterialIssueService
{
    Task<InventoryDocumentResponse> CreateAsync(CreateMaterialIssueRequest request, CancellationToken ct = default);
    Task<PagedResult<InventoryDocumentResponse>> ListAsync(InventoryDocFilter filter, CancellationToken ct = default);
    Task<InventoryDocumentResponse> GetAsync(Guid id, CancellationToken ct = default);
}

public sealed class MaterialIssueService : IMaterialIssueService
{
    private readonly IMaterialIssueRepository _issues;
    private readonly IWarehouseRepository _warehouses;
    private readonly IItemRepository _items;
    private readonly IStockLedger _ledger;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IOutboxWriter _outbox;
    private readonly IClock _clock;

    public MaterialIssueService(IMaterialIssueRepository issues, IWarehouseRepository warehouses, IItemRepository items,
        IStockLedger ledger, ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit, IOutboxWriter outbox, IClock clock)
    {
        _issues = issues;
        _warehouses = warehouses;
        _items = items;
        _ledger = ledger;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<InventoryDocumentResponse> CreateAsync(CreateMaterialIssueRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (!await _warehouses.ExistsAsync(orgId, request.WarehouseId, ct))
            throw new ConflictException("Warehouse does not belong to the organization.");
        foreach (var line in request.Lines)
            if (!await _items.ExistsAsync(orgId, line.ItemId, ct))
                throw new ConflictException($"Item {line.ItemId} does not belong to the organization.");

        var number = string.IsNullOrWhiteSpace(request.IssueNumber)
            ? InventoryNumbers.Next("ISS")
            : request.IssueNumber.Trim();
        if (await _issues.NumberExistsAsync(orgId, number, ct))
            throw new DuplicateEntityException($"A material issue with number '{number}' already exists.");

        var issue = new MaterialIssue
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            IssueNumber = number,
            WarehouseId = request.WarehouseId,
            ProjectId = request.ProjectId,
            WorkOrderId = request.WorkOrderId,
            IssueType = string.IsNullOrEmpty(request.IssueType) ? "PROJECT" : request.IssueType,
            IssueDate = request.IssueDate ?? DateOnly.FromDateTime(_clock.UtcNow),
            Status = "POSTED",
            Notes = request.Notes,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        decimal totalValue = 0;
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _issues.AddAsync(issue, token);

            foreach (var line in request.Lines)
            {
                var level = await _ledger.IssueAsync(line.ItemId, request.WarehouseId, line.Qty,
                    MovementTypes.Issue, RefDocTypes.MaterialIssue, issue.Id, line.BatchId, request.ProjectId, token);

                issue.Lines.Add(new MaterialIssueLine
                {
                    Id = Guid.NewGuid(),
                    MaterialIssueId = issue.Id,
                    ItemId = line.ItemId,
                    Qty = line.Qty,
                    UomId = line.UomId,
                    BatchId = line.BatchId,
                    UnitCost = level.AvgUnitCost
                });

                totalValue += line.Qty * level.AvgUnitCost;
                _outbox.Enqueue(new StockIssued(line.ItemId, request.WarehouseId, line.Qty, request.ProjectId) { OrganizationId = orgId });

                var item = await _items.GetByIdAsync(orgId, line.ItemId, false, token);
                if (item?.ReorderLevel is not null && level.QtyOnHand <= item.ReorderLevel)
                    _outbox.Enqueue(new ReorderLevelBreached(line.ItemId, request.WarehouseId, level.QtyOnHand, item.ReorderLevel.Value) { OrganizationId = orgId });
            }

            _audit.Add(EntityTypes.MaterialIssue, issue.Id, AuditActions.Create, null, new { issue.IssueNumber, LineCount = issue.Lines.Count });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return ToDocument(issue, totalValue);
    }

    public async Task<PagedResult<InventoryDocumentResponse>> ListAsync(InventoryDocFilter filter, CancellationToken ct = default)
    {
        var result = await _issues.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<InventoryDocumentResponse>
        {
            Items = result.Items.Select(i => ToDocument(i, 0)).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<InventoryDocumentResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var i = await _issues.GetAsync(_tenant.OrganizationId, id, ct) ?? throw NotFoundException.For("MaterialIssue", id);
        var total = i.Lines.Sum(l => l.Qty * l.UnitCost);
        return ToDocument(i, total);
    }

    private static InventoryDocumentResponse ToDocument(MaterialIssue i, decimal totalValue) => new()
    {
        Id = i.Id,
        Number = i.IssueNumber,
        DocumentType = RefDocTypes.MaterialIssue,
        WarehouseId = i.WarehouseId,
        ProjectId = i.ProjectId,
        Status = i.Status,
        DocumentDate = i.IssueDate,
        LineCount = i.Lines.Count,
        TotalValue = totalValue,
        CreatedAt = i.CreatedAt
    };
}

public interface IStockTransferService
{
    Task<InventoryDocumentResponse> CreateAsync(CreateStockTransferRequest request, CancellationToken ct = default);
    Task<PagedResult<InventoryDocumentResponse>> ListAsync(InventoryDocFilter filter, CancellationToken ct = default);
    Task<InventoryDocumentResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<InventoryDocumentResponse> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<InventoryDocumentResponse> DispatchAsync(Guid id, CancellationToken ct = default);
    Task<InventoryDocumentResponse> ReceiveAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Staged lifecycle: Requested (no stock impact) -> Approved -> Dispatched (deducts source stock,
/// captures unit cost per line) -> Received (adds stock to destination at the captured cost).
/// </summary>
public sealed class StockTransferService : IStockTransferService
{
    private readonly IStockTransferRepository _transfers;
    private readonly IWarehouseRepository _warehouses;
    private readonly IItemRepository _items;
    private readonly IStockLedger _ledger;
    private readonly IProjectStatusAdvancer _statusAdvancer;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IOutboxWriter _outbox;
    private readonly IClock _clock;

    public StockTransferService(IStockTransferRepository transfers, IWarehouseRepository warehouses, IItemRepository items,
        IStockLedger ledger, IProjectStatusAdvancer statusAdvancer, ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit,
        IOutboxWriter outbox, IClock clock)
    {
        _transfers = transfers;
        _warehouses = warehouses;
        _items = items;
        _ledger = ledger;
        _statusAdvancer = statusAdvancer;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<InventoryDocumentResponse> CreateAsync(CreateStockTransferRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (request.FromWarehouseId.HasValue && !await _warehouses.ExistsAsync(orgId, request.FromWarehouseId.Value, ct))
            throw new ConflictException("Source warehouse does not belong to the organization.");
        if (!await _warehouses.ExistsAsync(orgId, request.ToWarehouseId, ct))
            throw new ConflictException("Destination warehouse does not belong to the organization.");
        foreach (var line in request.Lines)
            if (!await _items.ExistsAsync(orgId, line.ItemId, ct))
                throw new ConflictException($"Item {line.ItemId} does not belong to the organization.");

        var number = string.IsNullOrWhiteSpace(request.TransferNumber)
            ? InventoryNumbers.Next("TRF")
            : request.TransferNumber.Trim();
        if (await _transfers.NumberExistsAsync(orgId, number, ct))
            throw new DuplicateEntityException($"A stock transfer with number '{number}' already exists.");

        var transfer = new StockTransfer
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            TransferNumber = number,
            FromWarehouseId = request.FromWarehouseId,
            SourceAddress = request.SourceAddress?.Trim(),
            ToWarehouseId = request.ToWarehouseId,
            ProjectId = request.ProjectId,
            MaterialRequirementId = request.MaterialRequirementId,
            TransportId = request.TransportId?.Trim(),
            Status = TransferStatuses.Requested,
            TransferDate = request.TransferDate ?? DateOnly.FromDateTime(_clock.UtcNow),
            Notes = request.Notes,
            RequestedBy = _tenant.UserId,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _transfers.AddAsync(transfer, token);
            foreach (var line in request.Lines)
            {
                transfer.Lines.Add(new StockTransferLine
                {
                    Id = Guid.NewGuid(),
                    StockTransferId = transfer.Id,
                    ItemId = line.ItemId,
                    Qty = line.Qty,
                    UomId = line.UomId,
                    BatchId = line.BatchId
                });
            }

            _audit.Add(EntityTypes.StockTransfer, transfer.Id, AuditActions.Create, null, new { transfer.TransferNumber, LineCount = transfer.Lines.Count });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return ToDocument(transfer, 0);
    }

    public async Task<PagedResult<InventoryDocumentResponse>> ListAsync(InventoryDocFilter filter, CancellationToken ct = default)
    {
        var result = await _transfers.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<InventoryDocumentResponse>
        {
            Items = result.Items.Select(t => ToDocument(t, 0)).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<InventoryDocumentResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _transfers.GetAsync(_tenant.OrganizationId, id, track: false, ct) ?? throw NotFoundException.For("StockTransfer", id);
        return ToDocument(t, t.Lines.Sum(l => l.Qty * l.UnitCost));
    }

    public async Task<InventoryDocumentResponse> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var t = await RequireStatusAsync(id, TransferStatuses.Requested, ct);
        t.Status = TransferStatuses.Approved;
        t.ApprovedBy = _tenant.UserId;
        t.ApprovedAt = _clock.UtcNow;
        return await SaveStatusAsync(t, 0, ct);
    }

    public async Task<InventoryDocumentResponse> DispatchAsync(Guid id, CancellationToken ct = default)
    {
        var t = await RequireStatusAsync(id, TransferStatuses.Approved, ct);
        decimal totalValue = 0;
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            foreach (var line in t.Lines)
            {
                if (t.FromWarehouseId.HasValue)
                {
                    var fromLevel = await _ledger.IssueAsync(line.ItemId, t.FromWarehouseId.Value, line.Qty,
                        MovementTypes.TransferOut, RefDocTypes.StockTransfer, t.Id, line.BatchId, null, token);
                    line.UnitCost = fromLevel.AvgUnitCost;
                }
                else
                {
                    // No internal warehouse to issue from (e.g. sourced directly from a supplier) — value at standard cost.
                    var item = await _items.GetByIdAsync(t.OrganizationId, line.ItemId, track: false, token);
                    line.UnitCost = item?.StandardCost ?? 0;
                }
                totalValue += line.Qty * line.UnitCost;
            }

            t.Status = TransferStatuses.Dispatched;
            t.DispatchedBy = _tenant.UserId;
            t.DispatchedAt = _clock.UtcNow;
            t.UpdatedAt = _clock.UtcNow;
            t.UpdatedBy = _tenant.UserId;
            _audit.Add(EntityTypes.StockTransfer, t.Id, AuditActions.StatusChange, null, new { t.TransferNumber, t.Status });
            await _statusAdvancer.AdvanceAsync(t.OrganizationId, t.ProjectId, "INVENTORY_CHECK", "SHIPMENT_IN_TRANSIT", token);
            await _uow.SaveChangesAsync(token);
        }, ct);

        return ToDocument(t, totalValue);
    }

    public async Task<InventoryDocumentResponse> ReceiveAsync(Guid id, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var t = await RequireStatusAsync(id, TransferStatuses.Dispatched, ct);
        decimal totalValue = 0;
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            foreach (var line in t.Lines)
            {
                await _ledger.ReceiveAsync(line.ItemId, t.ToWarehouseId, line.Qty, line.UnitCost,
                    MovementTypes.TransferIn, RefDocTypes.StockTransfer, t.Id, line.BatchId, null, token);
                totalValue += line.Qty * line.UnitCost;
                _outbox.Enqueue(new StockTransferred(line.ItemId, t.FromWarehouseId, t.ToWarehouseId, line.Qty) { OrganizationId = orgId });
            }

            t.Status = TransferStatuses.Received;
            t.ReceivedBy = _tenant.UserId;
            t.ReceivedAt = _clock.UtcNow;
            t.UpdatedAt = _clock.UtcNow;
            t.UpdatedBy = _tenant.UserId;
            _audit.Add(EntityTypes.StockTransfer, t.Id, AuditActions.StatusChange, null, new { t.TransferNumber, t.Status });
            await _statusAdvancer.AdvanceAsync(orgId, t.ProjectId, "SHIPMENT_IN_TRANSIT", "SHIPMENT_COMPLETED", token);
            await _uow.SaveChangesAsync(token);
        }, ct);

        return ToDocument(t, totalValue);
    }

    private async Task<StockTransfer> RequireStatusAsync(Guid id, string requiredStatus, CancellationToken ct)
    {
        var t = await _transfers.GetAsync(_tenant.OrganizationId, id, track: true, ct) ?? throw NotFoundException.For("StockTransfer", id);
        if (t.Status != requiredStatus)
            throw new ConflictException($"Stock transfer must be in status '{requiredStatus}' (current status: {t.Status}).");
        return t;
    }

    private async Task<InventoryDocumentResponse> SaveStatusAsync(StockTransfer t, decimal totalValue, CancellationToken ct)
    {
        t.UpdatedAt = _clock.UtcNow;
        t.UpdatedBy = _tenant.UserId;
        _audit.Add(EntityTypes.StockTransfer, t.Id, AuditActions.StatusChange, null, new { t.TransferNumber, t.Status });
        await _uow.SaveChangesAsync(ct);
        return ToDocument(t, totalValue);
    }

    private static InventoryDocumentResponse ToDocument(StockTransfer t, decimal totalValue) => new()
    {
        Id = t.Id,
        Number = t.TransferNumber,
        DocumentType = RefDocTypes.StockTransfer,
        WarehouseId = t.FromWarehouseId,
        SourceAddress = t.SourceAddress,
        ToWarehouseId = t.ToWarehouseId,
        ProjectId = t.ProjectId,
        MaterialRequirementId = t.MaterialRequirementId,
        Status = t.Status,
        DocumentDate = t.TransferDate,
        Reference = t.TransportId,
        LineCount = t.Lines.Count,
        TotalValue = totalValue,
        CreatedAt = t.CreatedAt,
        Lines = t.Lines.Select(l => new InventoryDocumentLineResponse { ItemId = l.ItemId, Qty = l.Qty, UomId = l.UomId }).ToList(),
        RequestedBy = t.RequestedBy,
        ApprovedBy = t.ApprovedBy,
        ApprovedAt = t.ApprovedAt,
        DispatchedBy = t.DispatchedBy,
        DispatchedAt = t.DispatchedAt,
        ReceivedBy = t.ReceivedBy,
        ReceivedAt = t.ReceivedAt
    };
}

public interface IStockAdjustmentService
{
    Task<InventoryDocumentResponse> CreateAsync(CreateStockAdjustmentRequest request, CancellationToken ct = default);
    Task<PagedResult<InventoryDocumentResponse>> ListAsync(InventoryDocFilter filter, CancellationToken ct = default);
    Task<InventoryDocumentResponse> GetAsync(Guid id, CancellationToken ct = default);
}

public sealed class StockAdjustmentService : IStockAdjustmentService
{
    private readonly IStockAdjustmentRepository _adjustments;
    private readonly IWarehouseRepository _warehouses;
    private readonly IItemRepository _items;
    private readonly IStockLedger _ledger;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IOutboxWriter _outbox;
    private readonly IClock _clock;

    public StockAdjustmentService(IStockAdjustmentRepository adjustments, IWarehouseRepository warehouses, IItemRepository items,
        IStockLedger ledger, ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit, IOutboxWriter outbox, IClock clock)
    {
        _adjustments = adjustments;
        _warehouses = warehouses;
        _items = items;
        _ledger = ledger;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<InventoryDocumentResponse> CreateAsync(CreateStockAdjustmentRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (!await _warehouses.ExistsAsync(orgId, request.WarehouseId, ct))
            throw new ConflictException("Warehouse does not belong to the organization.");
        foreach (var line in request.Lines)
            if (!await _items.ExistsAsync(orgId, line.ItemId, ct))
                throw new ConflictException($"Item {line.ItemId} does not belong to the organization.");

        var number = string.IsNullOrWhiteSpace(request.AdjustmentNumber)
            ? InventoryNumbers.Next("ADJ")
            : request.AdjustmentNumber.Trim();
        if (await _adjustments.NumberExistsAsync(orgId, number, ct))
            throw new DuplicateEntityException($"A stock adjustment with number '{number}' already exists.");

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            AdjustmentNumber = number,
            WarehouseId = request.WarehouseId,
            ReasonCode = request.ReasonCode,
            AdjustmentDate = request.AdjustmentDate ?? DateOnly.FromDateTime(_clock.UtcNow),
            Status = "POSTED",
            Notes = request.Notes,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _adjustments.AddAsync(adjustment, token);

            foreach (var line in request.Lines)
            {
                decimal unitCost;
                if (line.QtyDelta > 0)
                {
                    var item = await _items.GetByIdAsync(orgId, line.ItemId, false, token);
                    unitCost = line.UnitCost ?? item?.StandardCost ?? 0;
                    await _ledger.ReceiveAsync(line.ItemId, request.WarehouseId, line.QtyDelta, unitCost,
                        MovementTypes.Adjustment, RefDocTypes.StockAdjustment, adjustment.Id, line.BatchId, null, token);
                }
                else
                {
                    var level = await _ledger.IssueAsync(line.ItemId, request.WarehouseId, Math.Abs(line.QtyDelta),
                        MovementTypes.Adjustment, RefDocTypes.StockAdjustment, adjustment.Id, line.BatchId, null, token);
                    unitCost = level.AvgUnitCost;
                }

                adjustment.Lines.Add(new StockAdjustmentLine
                {
                    Id = Guid.NewGuid(),
                    StockAdjustmentId = adjustment.Id,
                    ItemId = line.ItemId,
                    QtyDelta = line.QtyDelta,
                    UomId = line.UomId,
                    BatchId = line.BatchId,
                    UnitCost = unitCost
                });

                _outbox.Enqueue(new StockAdjusted(line.ItemId, request.WarehouseId, line.QtyDelta, request.ReasonCode) { OrganizationId = orgId });
            }

            _audit.Add(EntityTypes.StockAdjustment, adjustment.Id, AuditActions.Create, null, new { adjustment.AdjustmentNumber, adjustment.ReasonCode });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return ToDocument(adjustment);
    }

    public async Task<PagedResult<InventoryDocumentResponse>> ListAsync(InventoryDocFilter filter, CancellationToken ct = default)
    {
        var result = await _adjustments.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<InventoryDocumentResponse>
        {
            Items = result.Items.Select(ToDocument).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<InventoryDocumentResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var a = await _adjustments.GetAsync(_tenant.OrganizationId, id, ct) ?? throw NotFoundException.For("StockAdjustment", id);
        return ToDocument(a);
    }

    private static InventoryDocumentResponse ToDocument(StockAdjustment a) => new()
    {
        Id = a.Id,
        Number = a.AdjustmentNumber,
        DocumentType = RefDocTypes.StockAdjustment,
        WarehouseId = a.WarehouseId,
        Status = a.Status,
        DocumentDate = a.AdjustmentDate,
        LineCount = a.Lines.Count,
        TotalValue = a.Lines.Sum(l => Math.Abs(l.QtyDelta) * l.UnitCost),
        CreatedAt = a.CreatedAt
    };
}

internal static class InventoryNumbers
{
    public static string Next(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
}
