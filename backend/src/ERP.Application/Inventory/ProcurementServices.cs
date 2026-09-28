using ERP.Application.Abstractions;
using ERP.Application.Projects;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Domain.Events;
using ERP.Shared.Exceptions;
using ERP.Shared.Pagination;

namespace ERP.Application.Inventory;

internal static class ProcurementMappers
{
    public static MaterialRequirementResponse ToResponse(MaterialRequirement r) => new()
    {
        Id = r.Id,
        ReqNumber = r.ReqNumber,
        ProjectId = r.ProjectId,
        WarehouseId = r.WarehouseId,
        DestinationAddress = r.DestinationAddress,
        DepartmentId = r.DepartmentId,
        Priority = r.Priority,
        Status = r.Status,
        RequiredDate = r.RequiredDate,
        RequestedBy = r.RequestedBy,
        ApprovedBy = r.ApprovedBy,
        ApprovedAt = r.ApprovedAt,
        RejectedBy = r.RejectedBy,
        RejectedAt = r.RejectedAt,
        RejectionReason = r.RejectionReason,
        PurchaseOrderId = r.PurchaseOrderId,
        Notes = r.Notes,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        RowVersion = r.RowVersion is null ? string.Empty : Convert.ToBase64String(r.RowVersion),
        Lines = r.Lines.Select(l => new MaterialRequirementLineResponse
        {
            Id = l.Id,
            ItemId = l.ItemId,
            Qty = l.Qty,
            UomId = l.UomId,
            Notes = l.Notes,
            PurchaseOrderLineId = l.PurchaseOrderLineId
        }).ToList()
    };

    public static PurchaseOrderResponse ToResponse(PurchaseOrder o) => new()
    {
        Id = o.Id,
        PoNumber = o.PoNumber,
        SupplierId = o.SupplierId,
        ProjectId = o.ProjectId,
        WarehouseId = o.WarehouseId,
        MaterialRequirementId = o.MaterialRequirementId,
        Status = o.Status,
        OrderDate = o.OrderDate,
        ExpectedDate = o.ExpectedDate,
        CurrencyCode = o.CurrencyCode,
        SubTotal = o.SubTotal,
        TaxAmount = o.TaxAmount,
        TotalAmount = o.TotalAmount,
        Notes = o.Notes,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt,
        RowVersion = o.RowVersion is null ? string.Empty : Convert.ToBase64String(o.RowVersion),
        Lines = o.Lines.Select(l => new PurchaseOrderLineResponse
        {
            Id = l.Id,
            ItemId = l.ItemId,
            Qty = l.Qty,
            UomId = l.UomId,
            UnitPrice = l.UnitPrice,
            TaxRatePercent = l.TaxRatePercent,
            TaxAmount = l.TaxAmount,
            LineTotal = l.LineTotal,
            QtyReceived = l.QtyReceived,
            MaterialRequirementLineId = l.MaterialRequirementLineId
        }).ToList()
    };

    public static PurchaseOrderLine BuildLine(Guid poId, Guid itemId, decimal qty, Guid? uomId, decimal unitPrice,
        decimal taxRatePercent, Guid? materialRequirementLineId) => new()
    {
        Id = Guid.NewGuid(),
        PurchaseOrderId = poId,
        ItemId = itemId,
        Qty = qty,
        UomId = uomId,
        UnitPrice = unitPrice,
        TaxRatePercent = taxRatePercent,
        TaxAmount = Math.Round(qty * unitPrice * taxRatePercent / 100m, 4),
        LineTotal = Math.Round(qty * unitPrice, 4) + Math.Round(qty * unitPrice * taxRatePercent / 100m, 4),
        QtyReceived = 0,
        MaterialRequirementLineId = materialRequirementLineId
    };
}

public interface IMaterialRequirementService
{
    Task<MaterialRequirementResponse> CreateAsync(CreateMaterialRequirementRequest request, CancellationToken ct = default);
    Task<PagedResult<MaterialRequirementResponse>> ListAsync(MaterialRequirementFilter filter, CancellationToken ct = default);
    Task<MaterialRequirementResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<MaterialRequirementResponse> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<MaterialRequirementResponse> RejectAsync(Guid id, RejectMaterialRequirementRequest request, CancellationToken ct = default);
    Task<PurchaseOrderResponse> ConvertToPurchaseOrderAsync(Guid id, ConvertToPurchaseOrderRequest request, CancellationToken ct = default);
}

public sealed class MaterialRequirementService : IMaterialRequirementService
{
    private readonly IMaterialRequirementRepository _requirements;
    private readonly IPurchaseOrderRepository _purchaseOrders;
    private readonly IWarehouseRepository _warehouses;
    private readonly ISupplierRepository _suppliers;
    private readonly IItemRepository _items;
    private readonly IProjectStatusAdvancer _statusAdvancer;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IOutboxWriter _outbox;
    private readonly IClock _clock;

    public MaterialRequirementService(IMaterialRequirementRepository requirements, IPurchaseOrderRepository purchaseOrders,
        IWarehouseRepository warehouses, ISupplierRepository suppliers, IItemRepository items,
        IProjectStatusAdvancer statusAdvancer, ITenantContext tenant,
        IUnitOfWork uow, IAuditWriter audit, IOutboxWriter outbox, IClock clock)
    {
        _requirements = requirements;
        _purchaseOrders = purchaseOrders;
        _warehouses = warehouses;
        _suppliers = suppliers;
        _items = items;
        _statusAdvancer = statusAdvancer;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<MaterialRequirementResponse> CreateAsync(CreateMaterialRequirementRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (request.WarehouseId.HasValue && !await _warehouses.ExistsAsync(orgId, request.WarehouseId.Value, ct))
            throw new ConflictException("Site/warehouse does not belong to the organization.");
        foreach (var line in request.Lines)
            if (!await _items.ExistsAsync(orgId, line.ItemId, ct))
                throw new ConflictException($"Item {line.ItemId} does not belong to the organization.");

        var number = string.IsNullOrWhiteSpace(request.ReqNumber)
            ? InventoryNumbers.Next("MREQ")
            : request.ReqNumber.Trim();
        if (await _requirements.NumberExistsAsync(orgId, number, ct))
            throw new DuplicateEntityException($"A material requirement with number '{number}' already exists.");

        var requirement = new MaterialRequirement
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ReqNumber = number,
            ProjectId = request.ProjectId,
            WarehouseId = request.WarehouseId,
            DestinationAddress = request.DestinationAddress,
            DepartmentId = request.DepartmentId,
            Priority = string.IsNullOrEmpty(request.Priority) ? Priorities.Medium : request.Priority,
            Status = MaterialRequirementStatuses.Submitted,
            RequiredDate = request.RequiredDate,
            RequestedBy = _tenant.UserId,
            Notes = request.Notes,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _requirements.AddAsync(requirement, token);
            foreach (var line in request.Lines)
            {
                requirement.Lines.Add(new MaterialRequirementLine
                {
                    Id = Guid.NewGuid(),
                    MaterialRequirementId = requirement.Id,
                    ItemId = line.ItemId,
                    Qty = line.Qty,
                    UomId = line.UomId,
                    Notes = line.Notes
                });
            }
            _audit.Add(EntityTypes.MaterialRequirement, requirement.Id, AuditActions.Create, null,
                new { requirement.ReqNumber, LineCount = requirement.Lines.Count });
            await _statusAdvancer.AdvanceAsync(orgId, requirement.ProjectId, "PLANNING", "INVENTORY_CHECK", token);
            await _uow.SaveChangesAsync(token);
        }, ct);

        return ProcurementMappers.ToResponse(requirement);
    }

    public async Task<PagedResult<MaterialRequirementResponse>> ListAsync(MaterialRequirementFilter filter, CancellationToken ct = default)
    {
        var result = await _requirements.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<MaterialRequirementResponse>
        {
            Items = result.Items.Select(ProcurementMappers.ToResponse).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<MaterialRequirementResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _requirements.GetAsync(_tenant.OrganizationId, id, track: false, ct)
                 ?? throw NotFoundException.For("MaterialRequirement", id);
        return ProcurementMappers.ToResponse(r);
    }

    public async Task<MaterialRequirementResponse> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var r = await _requirements.GetAsync(orgId, id, track: true, ct) ?? throw NotFoundException.For("MaterialRequirement", id);
        if (r.Status != MaterialRequirementStatuses.Submitted)
            throw new ConflictException($"Only submitted requirements can be approved (current status: {r.Status}).");

        r.Status = MaterialRequirementStatuses.Approved;
        r.ApprovedBy = _tenant.UserId;
        r.ApprovedAt = _clock.UtcNow;
        r.UpdatedAt = _clock.UtcNow;
        r.UpdatedBy = _tenant.UserId;

        _audit.Add(EntityTypes.MaterialRequirement, r.Id, AuditActions.Approve, null, new { r.ReqNumber });
        _outbox.Enqueue(new MaterialRequirementApproved(r.Id, r.ProjectId) { OrganizationId = orgId });
        await _uow.SaveChangesAsync(ct);
        return ProcurementMappers.ToResponse(r);
    }

    public async Task<MaterialRequirementResponse> RejectAsync(Guid id, RejectMaterialRequirementRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var r = await _requirements.GetAsync(orgId, id, track: true, ct) ?? throw NotFoundException.For("MaterialRequirement", id);
        if (r.Status != MaterialRequirementStatuses.Submitted)
            throw new ConflictException($"Only submitted requirements can be rejected (current status: {r.Status}).");

        r.Status = MaterialRequirementStatuses.Rejected;
        r.RejectedBy = _tenant.UserId;
        r.RejectedAt = _clock.UtcNow;
        r.RejectionReason = request.Reason;
        r.UpdatedAt = _clock.UtcNow;
        r.UpdatedBy = _tenant.UserId;

        _audit.Add(EntityTypes.MaterialRequirement, r.Id, AuditActions.Reject, null, new { r.ReqNumber, request.Reason });
        _outbox.Enqueue(new MaterialRequirementRejected(r.Id, r.ProjectId, request.Reason) { OrganizationId = orgId });
        await _uow.SaveChangesAsync(ct);
        return ProcurementMappers.ToResponse(r);
    }

    public async Task<PurchaseOrderResponse> ConvertToPurchaseOrderAsync(Guid id, ConvertToPurchaseOrderRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var r = await _requirements.GetAsync(orgId, id, track: true, ct) ?? throw NotFoundException.For("MaterialRequirement", id);
        if (r.Status != MaterialRequirementStatuses.Approved)
            throw new ConflictException($"Only approved requirements can be converted to a purchase order (current status: {r.Status}).");
        if (!await _suppliers.ExistsAsync(orgId, request.SupplierId, ct))
            throw new ConflictException("Supplier does not belong to the organization.");

        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            PoNumber = InventoryNumbers.Next("PO"),
            SupplierId = request.SupplierId,
            ProjectId = r.ProjectId,
            WarehouseId = request.WarehouseId ?? r.WarehouseId,
            MaterialRequirementId = r.Id,
            Status = PurchaseOrderStatuses.Draft,
            OrderDate = DateOnly.FromDateTime(_clock.UtcNow),
            ExpectedDate = request.ExpectedDate,
            CurrencyCode = request.CurrencyCode,
            Notes = request.Notes,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _purchaseOrders.AddAsync(po, token);

            foreach (var lineRequest in request.Lines)
            {
                var reqLine = r.Lines.FirstOrDefault(l => l.Id == lineRequest.MaterialRequirementLineId)
                    ?? throw new ConflictException($"Requirement line {lineRequest.MaterialRequirementLineId} not found on this requirement.");
                if (reqLine.PurchaseOrderLineId.HasValue)
                    throw new ConflictException($"Requirement line {reqLine.Id} has already been converted to a purchase order.");

                var poLine = ProcurementMappers.BuildLine(po.Id, reqLine.ItemId, reqLine.Qty, reqLine.UomId,
                    lineRequest.UnitPrice, lineRequest.TaxRatePercent, reqLine.Id);
                po.Lines.Add(poLine);
            }

            po.SubTotal = po.Lines.Sum(l => Math.Round(l.Qty * l.UnitPrice, 4));
            po.TaxAmount = po.Lines.Sum(l => l.TaxAmount);
            po.TotalAmount = po.SubTotal + po.TaxAmount;

            _audit.Add(EntityTypes.PurchaseOrder, po.Id, AuditActions.Create, null, new { po.PoNumber, LineCount = po.Lines.Count });
            // PurchaseOrder must be inserted before MaterialRequirement(Lines) reference its Id (FK), hence two SaveChanges.
            await _uow.SaveChangesAsync(token);

            foreach (var poLine in po.Lines)
            {
                var reqLine = r.Lines.First(l => l.Id == poLine.MaterialRequirementLineId);
                reqLine.PurchaseOrderLineId = poLine.Id;
            }

            r.Status = MaterialRequirementStatuses.Converted;
            r.PurchaseOrderId = po.Id;
            r.UpdatedAt = _clock.UtcNow;
            r.UpdatedBy = _tenant.UserId;

            _audit.Add(EntityTypes.MaterialRequirement, r.Id, AuditActions.Update, null, new { Action = "ConvertedToPO", po.PoNumber });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return ProcurementMappers.ToResponse(po);
    }
}

public interface IPurchaseOrderService
{
    Task<PurchaseOrderResponse> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default);
    Task<PagedResult<PurchaseOrderResponse>> ListAsync(PurchaseOrderFilter filter, CancellationToken ct = default);
    Task<PurchaseOrderResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderResponse> UpdateAsync(Guid id, UpdatePurchaseOrderRequest request, CancellationToken ct = default);
    Task<PurchaseOrderResponse> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderResponse> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderResponse> RejectAsync(Guid id, RejectPurchaseOrderRequest request, CancellationToken ct = default);
    Task<PurchaseOrderResponse> MarkOrderedAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderResponse> CloseAsync(Guid id, CancellationToken ct = default);
}

public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _orders;
    private readonly ISupplierRepository _suppliers;
    private readonly IWarehouseRepository _warehouses;
    private readonly IItemRepository _items;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IOutboxWriter _outbox;
    private readonly IClock _clock;

    public PurchaseOrderService(IPurchaseOrderRepository orders, ISupplierRepository suppliers, IWarehouseRepository warehouses,
        IItemRepository items, ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit, IOutboxWriter outbox, IClock clock)
    {
        _orders = orders;
        _suppliers = suppliers;
        _warehouses = warehouses;
        _items = items;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<PurchaseOrderResponse> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (!await _suppliers.ExistsAsync(orgId, request.SupplierId, ct))
            throw new ConflictException("Supplier does not belong to the organization.");
        if (request.WarehouseId.HasValue && !await _warehouses.ExistsAsync(orgId, request.WarehouseId.Value, ct))
            throw new ConflictException("Warehouse does not belong to the organization.");
        foreach (var line in request.Lines)
            if (!await _items.ExistsAsync(orgId, line.ItemId, ct))
                throw new ConflictException($"Item {line.ItemId} does not belong to the organization.");

        var number = string.IsNullOrWhiteSpace(request.PoNumber)
            ? InventoryNumbers.Next("PO")
            : request.PoNumber.Trim();
        if (await _orders.NumberExistsAsync(orgId, number, ct))
            throw new DuplicateEntityException($"A purchase order with number '{number}' already exists.");

        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            PoNumber = number,
            SupplierId = request.SupplierId,
            ProjectId = request.ProjectId,
            WarehouseId = request.WarehouseId,
            Status = PurchaseOrderStatuses.Draft,
            OrderDate = request.OrderDate ?? DateOnly.FromDateTime(_clock.UtcNow),
            ExpectedDate = request.ExpectedDate,
            CurrencyCode = request.CurrencyCode,
            Notes = request.Notes,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _orders.AddAsync(po, token);
            foreach (var line in request.Lines)
                po.Lines.Add(ProcurementMappers.BuildLine(po.Id, line.ItemId, line.Qty, line.UomId, line.UnitPrice, line.TaxRatePercent, null));

            po.SubTotal = po.Lines.Sum(l => Math.Round(l.Qty * l.UnitPrice, 4));
            po.TaxAmount = po.Lines.Sum(l => l.TaxAmount);
            po.TotalAmount = po.SubTotal + po.TaxAmount;

            _audit.Add(EntityTypes.PurchaseOrder, po.Id, AuditActions.Create, null, new { po.PoNumber, LineCount = po.Lines.Count });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return ProcurementMappers.ToResponse(po);
    }

    public async Task<PagedResult<PurchaseOrderResponse>> ListAsync(PurchaseOrderFilter filter, CancellationToken ct = default)
    {
        var result = await _orders.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<PurchaseOrderResponse>
        {
            Items = result.Items.Select(ProcurementMappers.ToResponse).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<PurchaseOrderResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var o = await _orders.GetAsync(_tenant.OrganizationId, id, track: false, ct) ?? throw NotFoundException.For("PurchaseOrder", id);
        return ProcurementMappers.ToResponse(o);
    }

    public async Task<PurchaseOrderResponse> UpdateAsync(Guid id, UpdatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var o = await _orders.GetAsync(orgId, id, track: true, ct) ?? throw NotFoundException.For("PurchaseOrder", id);
        if (o.Status != PurchaseOrderStatuses.Draft)
            throw new ConflictException($"Only draft purchase orders can be edited (current status: {o.Status}).");
        if (!await _suppliers.ExistsAsync(orgId, request.SupplierId, ct))
            throw new ConflictException("Supplier does not belong to the organization.");
        foreach (var line in request.Lines)
            if (!await _items.ExistsAsync(orgId, line.ItemId, ct))
                throw new ConflictException($"Item {line.ItemId} does not belong to the organization.");

        if (!string.IsNullOrEmpty(request.RowVersion))
            o.RowVersion = Convert.FromBase64String(request.RowVersion);

        o.SupplierId = request.SupplierId;
        o.ProjectId = request.ProjectId;
        o.WarehouseId = request.WarehouseId;
        o.OrderDate = request.OrderDate;
        o.ExpectedDate = request.ExpectedDate;
        o.CurrencyCode = request.CurrencyCode;
        o.Notes = request.Notes;
        o.UpdatedAt = _clock.UtcNow;
        o.UpdatedBy = _tenant.UserId;

        o.Lines.Clear();
        foreach (var line in request.Lines)
            o.Lines.Add(ProcurementMappers.BuildLine(o.Id, line.ItemId, line.Qty, line.UomId, line.UnitPrice, line.TaxRatePercent, null));

        o.SubTotal = o.Lines.Sum(l => Math.Round(l.Qty * l.UnitPrice, 4));
        o.TaxAmount = o.Lines.Sum(l => l.TaxAmount);
        o.TotalAmount = o.SubTotal + o.TaxAmount;

        _audit.Add(EntityTypes.PurchaseOrder, o.Id, AuditActions.Update, null, new { o.PoNumber });
        await _uow.SaveChangesAsync(ct);
        return ProcurementMappers.ToResponse(o);
    }

    public async Task<PurchaseOrderResponse> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var o = await RequireStatusAsync(id, PurchaseOrderStatuses.Draft, ct);
        o.Status = PurchaseOrderStatuses.PendingApproval;
        o.SubmittedBy = _tenant.UserId;
        o.SubmittedAt = _clock.UtcNow;
        return await SaveStatusAsync(o, ct);
    }

    public async Task<PurchaseOrderResponse> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var o = await RequireStatusAsync(id, PurchaseOrderStatuses.PendingApproval, ct);
        o.Status = PurchaseOrderStatuses.Approved;
        o.ApprovedBy = _tenant.UserId;
        o.ApprovedAt = _clock.UtcNow;
        return await SaveStatusAsync(o, ct, AuditActions.Approve);
    }

    public async Task<PurchaseOrderResponse> RejectAsync(Guid id, RejectPurchaseOrderRequest request, CancellationToken ct = default)
    {
        var o = await RequireStatusAsync(id, PurchaseOrderStatuses.PendingApproval, ct);
        o.Status = PurchaseOrderStatuses.Rejected;
        o.RejectedBy = _tenant.UserId;
        o.RejectedAt = _clock.UtcNow;
        o.RejectionReason = request.Reason;
        return await SaveStatusAsync(o, ct, AuditActions.Reject);
    }

    public async Task<PurchaseOrderResponse> MarkOrderedAsync(Guid id, CancellationToken ct = default)
    {
        var o = await RequireStatusAsync(id, PurchaseOrderStatuses.Approved, ct);
        o.Status = PurchaseOrderStatuses.Ordered;
        return await SaveStatusAsync(o, ct);
    }

    public async Task<PurchaseOrderResponse> CloseAsync(Guid id, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var o = await _orders.GetAsync(orgId, id, track: true, ct) ?? throw NotFoundException.For("PurchaseOrder", id);
        if (o.Status is not (PurchaseOrderStatuses.Ordered or PurchaseOrderStatuses.PartiallyReceived or PurchaseOrderStatuses.Received))
            throw new ConflictException($"Only ordered/received purchase orders can be closed (current status: {o.Status}).");
        o.Status = PurchaseOrderStatuses.Closed;
        o.ClosedAt = _clock.UtcNow;
        return await SaveStatusAsync(o, ct);
    }

    private async Task<PurchaseOrder> RequireStatusAsync(Guid id, string requiredStatus, CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId;
        var o = await _orders.GetAsync(orgId, id, track: true, ct) ?? throw NotFoundException.For("PurchaseOrder", id);
        if (o.Status != requiredStatus)
            throw new ConflictException($"Purchase order must be in status '{requiredStatus}' (current status: {o.Status}).");
        return o;
    }

    private async Task<PurchaseOrderResponse> SaveStatusAsync(PurchaseOrder o, CancellationToken ct, string action = AuditActions.StatusChange)
    {
        o.UpdatedAt = _clock.UtcNow;
        o.UpdatedBy = _tenant.UserId;
        _audit.Add(EntityTypes.PurchaseOrder, o.Id, action, null, new { o.PoNumber, o.Status });
        _outbox.Enqueue(new PurchaseOrderStatusChanged(o.Id, o.Status) { OrganizationId = _tenant.OrganizationId });
        await _uow.SaveChangesAsync(ct);
        return ProcurementMappers.ToResponse(o);
    }
}
