using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Domain.Events;
using ERP.Shared.Exceptions;
using ERP.Shared.Pagination;

namespace ERP.Application.Inventory;

public interface IBomService
{
    Task<BomResponse> CreateAsync(CreateBomRequest request, CancellationToken ct = default);
    Task<PagedResult<BomResponse>> ListAsync(BomFilter filter, CancellationToken ct = default);
    Task<BomResponse> GetAsync(Guid id, CancellationToken ct = default);
}

public sealed class BomService : IBomService
{
    private readonly IBomRepository _boms;
    private readonly IItemRepository _items;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;

    public BomService(IBomRepository boms, IItemRepository items, ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit, IClock clock)
    {
        _boms = boms;
        _items = items;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _clock = clock;
    }

    public async Task<BomResponse> CreateAsync(CreateBomRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (await _boms.CodeExistsAsync(orgId, request.Code, null, ct))
            throw new DuplicateEntityException($"A BOM with code '{request.Code}' already exists.");
        if (!await _items.ExistsAsync(orgId, request.OutputItemId, ct))
            throw new ConflictException("Output item does not belong to the organization.");
        foreach (var line in request.Lines)
            if (!await _items.ExistsAsync(orgId, line.ComponentItemId, ct))
                throw new ConflictException($"Component item {line.ComponentItemId} does not belong to the organization.");

        var bom = new BillOfMaterials
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Code = request.Code,
            OutputItemId = request.OutputItemId,
            OutputQty = request.OutputQty,
            UomId = request.UomId,
            Version = request.Version,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };
        foreach (var line in request.Lines)
        {
            bom.Lines.Add(new BomLine
            {
                Id = Guid.NewGuid(),
                BillOfMaterialsId = bom.Id,
                ComponentItemId = line.ComponentItemId,
                Qty = line.Qty,
                UomId = line.UomId,
                ScrapPercent = line.ScrapPercent,
                Operation = line.Operation
            });
        }

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _boms.AddAsync(bom, token);
            _audit.Add(EntityTypes.Bom, bom.Id, AuditActions.Create, null, new { bom.Code, ComponentCount = bom.Lines.Count });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(bom);
    }

    public async Task<PagedResult<BomResponse>> ListAsync(BomFilter filter, CancellationToken ct = default)
    {
        var result = await _boms.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<BomResponse>
        {
            Items = result.Items.Select(Map).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<BomResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var bom = await _boms.GetAsync(_tenant.OrganizationId, id, false, ct) ?? throw NotFoundException.For("BOM", id);
        return Map(bom);
    }

    private static BomResponse Map(BillOfMaterials b) => new()
    {
        Id = b.Id,
        Code = b.Code,
        OutputItemId = b.OutputItemId,
        OutputQty = b.OutputQty,
        UomId = b.UomId,
        Version = b.Version,
        IsActive = b.IsActive,
        CreatedAt = b.CreatedAt,
        Lines = b.Lines.Select(l => new BomLineResponse
        {
            Id = l.Id, ComponentItemId = l.ComponentItemId, Qty = l.Qty, UomId = l.UomId,
            ScrapPercent = l.ScrapPercent, Operation = l.Operation
        }).ToList()
    };
}

public interface IWorkOrderService
{
    Task<WorkOrderResponse> CreateAsync(CreateWorkOrderRequest request, CancellationToken ct = default);
    Task<PagedResult<WorkOrderResponse>> ListAsync(WorkOrderFilter filter, CancellationToken ct = default);
    Task<WorkOrderResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<WorkOrderResponse> ReleaseAsync(Guid id, CancellationToken ct = default);
    Task<WorkOrderResponse> CompleteAsync(Guid id, CompleteWorkOrderRequest request, CancellationToken ct = default);
}

public sealed class WorkOrderService : IWorkOrderService
{
    private readonly IWorkOrderRepository _workOrders;
    private readonly IBomRepository _boms;
    private readonly IItemRepository _items;
    private readonly IWarehouseRepository _warehouses;
    private readonly IStockLedger _ledger;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IOutboxWriter _outbox;
    private readonly IClock _clock;

    public WorkOrderService(IWorkOrderRepository workOrders, IBomRepository boms, IItemRepository items,
        IWarehouseRepository warehouses, IStockLedger ledger, ITenantContext tenant, IUnitOfWork uow,
        IAuditWriter audit, IOutboxWriter outbox, IClock clock)
    {
        _workOrders = workOrders;
        _boms = boms;
        _items = items;
        _warehouses = warehouses;
        _ledger = ledger;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<WorkOrderResponse> CreateAsync(CreateWorkOrderRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (!await _items.ExistsAsync(orgId, request.OutputItemId, ct))
            throw new ConflictException("Output item does not belong to the organization.");
        if (!await _warehouses.ExistsAsync(orgId, request.WarehouseId, ct))
            throw new ConflictException("Warehouse does not belong to the organization.");

        BillOfMaterials? bom = null;
        if (request.BomId.HasValue)
        {
            bom = await _boms.GetAsync(orgId, request.BomId.Value, false, ct)
                ?? throw new ConflictException("BOM does not belong to the organization.");
            if (bom.OutputItemId != request.OutputItemId)
                throw new ConflictException("BOM output item does not match the work order output item.");
        }

        var number = string.IsNullOrWhiteSpace(request.WoNumber)
            ? InventoryNumbers.Next("WO")
            : request.WoNumber.Trim();
        if (await _workOrders.NumberExistsAsync(orgId, number, ct))
            throw new DuplicateEntityException($"A work order with number '{number}' already exists.");

        var wo = new WorkOrder
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            WoNumber = number,
            OutputItemId = request.OutputItemId,
            BomId = request.BomId,
            PlannedQty = request.PlannedQty,
            WarehouseId = request.WarehouseId,
            ProjectId = request.ProjectId,
            Status = WorkOrderStatuses.Draft,
            StartDate = request.StartDate,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        if (bom is not null && bom.OutputQty > 0)
        {
            var multiplier = request.PlannedQty / bom.OutputQty;
            foreach (var line in bom.Lines)
            {
                var planned = line.Qty * multiplier * (1 + (line.ScrapPercent / 100m));
                wo.Components.Add(new WorkOrderComponent
                {
                    Id = Guid.NewGuid(),
                    WorkOrderId = wo.Id,
                    ComponentItemId = line.ComponentItemId,
                    PlannedQty = planned,
                    ConsumedQty = 0,
                    UomId = line.UomId
                });
            }
        }

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _workOrders.AddAsync(wo, token);
            _audit.Add(EntityTypes.WorkOrder, wo.Id, AuditActions.Create, null, new { wo.WoNumber, wo.PlannedQty });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(wo);
    }

    public async Task<PagedResult<WorkOrderResponse>> ListAsync(WorkOrderFilter filter, CancellationToken ct = default)
    {
        var result = await _workOrders.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<WorkOrderResponse>
        {
            Items = result.Items.Select(Map).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<WorkOrderResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var wo = await _workOrders.GetAsync(_tenant.OrganizationId, id, false, ct) ?? throw NotFoundException.For("WorkOrder", id);
        return Map(wo);
    }

    public async Task<WorkOrderResponse> ReleaseAsync(Guid id, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var wo = await _workOrders.GetAsync(orgId, id, true, ct) ?? throw NotFoundException.For("WorkOrder", id);
        if (wo.Status != WorkOrderStatuses.Draft)
            throw new ConflictException($"Only DRAFT work orders can be released (current: {wo.Status}).");

        wo.Status = WorkOrderStatuses.Released;
        wo.UpdatedAt = _clock.UtcNow;
        wo.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.WorkOrder, wo.Id, AuditActions.StatusChange, null, new { Status = wo.Status });
            _outbox.Enqueue(new WorkOrderReleased(wo.Id, wo.WoNumber) { OrganizationId = orgId });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(wo);
    }

    public async Task<WorkOrderResponse> CompleteAsync(Guid id, CompleteWorkOrderRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var wo = await _workOrders.GetAsync(orgId, id, true, ct) ?? throw NotFoundException.For("WorkOrder", id);
        if (wo.Status != WorkOrderStatuses.Released)
            throw new ConflictException($"Only RELEASED work orders can be completed (current: {wo.Status}).");

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            decimal totalComponentCost = 0;

            foreach (var comp in wo.Components)
            {
                var toConsume = comp.PlannedQty - comp.ConsumedQty;
                if (toConsume <= 0) continue;

                var level = await _ledger.IssueAsync(comp.ComponentItemId, wo.WarehouseId, toConsume,
                    MovementTypes.Consumption, RefDocTypes.WorkOrder, wo.Id, null, wo.ProjectId, token);

                totalComponentCost += toConsume * level.AvgUnitCost;
                comp.ConsumedQty = comp.PlannedQty;
            }

            var unitCost = request.ProducedQty > 0 ? totalComponentCost / request.ProducedQty : 0;
            await _ledger.ReceiveAsync(wo.OutputItemId, wo.WarehouseId, request.ProducedQty, unitCost,
                MovementTypes.ProductionIn, RefDocTypes.WorkOrder, wo.Id, null, wo.ProjectId, token);

            wo.ProducedQty = request.ProducedQty;
            wo.ScrapQty = request.ScrapQty;
            wo.Status = WorkOrderStatuses.Completed;
            wo.EndDate = DateOnly.FromDateTime(_clock.UtcNow);
            wo.UpdatedAt = _clock.UtcNow;
            wo.UpdatedBy = _tenant.UserId;

            _audit.Add(EntityTypes.WorkOrder, wo.Id, AuditActions.StatusChange, null,
                new { Status = wo.Status, wo.ProducedQty, OutputUnitCost = unitCost });
            _outbox.Enqueue(new WorkOrderCompleted(wo.Id, wo.OutputItemId, request.ProducedQty) { OrganizationId = orgId });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(wo);
    }

    private static WorkOrderResponse Map(WorkOrder w) => new()
    {
        Id = w.Id,
        WoNumber = w.WoNumber,
        OutputItemId = w.OutputItemId,
        BomId = w.BomId,
        PlannedQty = w.PlannedQty,
        ProducedQty = w.ProducedQty,
        ScrapQty = w.ScrapQty,
        WarehouseId = w.WarehouseId,
        ProjectId = w.ProjectId,
        Status = w.Status,
        StartDate = w.StartDate,
        EndDate = w.EndDate,
        CreatedAt = w.CreatedAt,
        RowVersion = w.RowVersion is null ? string.Empty : Convert.ToBase64String(w.RowVersion),
        Components = w.Components.Select(c => new WorkOrderComponentResponse
        {
            Id = c.Id, ComponentItemId = c.ComponentItemId, PlannedQty = c.PlannedQty,
            ConsumedQty = c.ConsumedQty, UomId = c.UomId
        }).ToList()
    };
}
