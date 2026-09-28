using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using ERP.Domain.Entities;
using ERP.Domain.Events;
using ERP.Shared.Exceptions;
using ERP.Shared.Pagination;

namespace ERP.Application.Inventory;

public interface IItemService
{
    Task<ItemResponse> CreateAsync(CreateItemRequest request, CancellationToken ct = default);
    Task<PagedResult<ItemResponse>> ListAsync(ItemFilter filter, CancellationToken ct = default);
    Task<ItemResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<ItemResponse> UpdateAsync(Guid id, UpdateItemRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class ItemService : IItemService
{
    private readonly IItemRepository _items;
    private readonly IItemCategoryRepository _categories;
    private readonly IUnitOfMeasureRepository _uoms;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IOutboxWriter _outbox;
    private readonly IClock _clock;

    public ItemService(IItemRepository items, IItemCategoryRepository categories, IUnitOfMeasureRepository uoms,
        ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit, IOutboxWriter outbox, IClock clock)
    {
        _items = items;
        _categories = categories;
        _uoms = uoms;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task<ItemResponse> CreateAsync(CreateItemRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (await _items.CodeExistsAsync(orgId, request.Code, null, ct))
            throw new DuplicateEntityException($"An item with code '{request.Code}' already exists.");
        await ValidateReferencesAsync(request.CategoryId, request.BaseUomId, ct);

        var item = new Item
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            ItemType = request.ItemType,
            CategoryId = request.CategoryId,
            BaseUomId = request.BaseUomId,
            Barcode = request.Barcode,
            TrackBatches = request.TrackBatches,
            TrackSerials = request.TrackSerials,
            TrackExpiry = request.TrackExpiry,
            StandardCost = request.StandardCost,
            ReorderLevel = request.ReorderLevel,
            SafetyStock = request.SafetyStock,
            MinStock = request.MinStock,
            MaxStock = request.MaxStock,
            ReorderQty = request.ReorderQty,
            IsPurchasable = request.IsPurchasable,
            IsManufactured = request.IsManufactured,
            IsSellable = request.IsSellable,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _items.AddAsync(item, token);
            _audit.Add(EntityTypes.Item, item.Id, AuditActions.Create, null, new { item.Code, item.Name, item.ItemType });
            _outbox.Enqueue(new ItemCreated(item.Id, item.Code, item.Name, item.ItemType) { OrganizationId = orgId });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(item);
    }

    public async Task<PagedResult<ItemResponse>> ListAsync(ItemFilter filter, CancellationToken ct = default)
    {
        var result = await _items.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<ItemResponse>
        {
            Items = result.Items.Select(Map).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<ItemResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _items.GetByIdAsync(_tenant.OrganizationId, id, false, ct)
            ?? throw NotFoundException.For("Item", id);
        return Map(item);
    }

    public async Task<ItemResponse> UpdateAsync(Guid id, UpdateItemRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var item = await _items.GetByIdAsync(orgId, id, true, ct) ?? throw NotFoundException.For("Item", id);
        await ValidateReferencesAsync(request.CategoryId, request.BaseUomId, ct);

        if (!string.IsNullOrEmpty(request.RowVersion))
            item.RowVersion = Convert.FromBase64String(request.RowVersion);

        item.Name = request.Name;
        item.Description = request.Description;
        item.ItemType = request.ItemType;
        item.CategoryId = request.CategoryId;
        item.BaseUomId = request.BaseUomId;
        item.Barcode = request.Barcode;
        item.TrackBatches = request.TrackBatches;
        item.TrackSerials = request.TrackSerials;
        item.TrackExpiry = request.TrackExpiry;
        item.StandardCost = request.StandardCost;
        item.ReorderLevel = request.ReorderLevel;
        item.SafetyStock = request.SafetyStock;
        item.MinStock = request.MinStock;
        item.MaxStock = request.MaxStock;
        item.ReorderQty = request.ReorderQty;
        item.IsPurchasable = request.IsPurchasable;
        item.IsManufactured = request.IsManufactured;
        item.IsSellable = request.IsSellable;
        item.IsActive = request.IsActive;
        item.UpdatedAt = _clock.UtcNow;
        item.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.Item, item.Id, AuditActions.Update, null, new { item.Name });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(item);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _items.GetByIdAsync(_tenant.OrganizationId, id, true, ct) ?? throw NotFoundException.For("Item", id);
        item.IsDeleted = true;
        item.DeletedAt = _clock.UtcNow;
        item.DeletedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.Item, item.Id, AuditActions.Delete);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    private async Task ValidateReferencesAsync(Guid? categoryId, Guid? uomId, CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId;
        if (categoryId.HasValue && !await _categories.ExistsAsync(orgId, categoryId.Value, ct))
            throw new ConflictException("Category does not belong to the organization.");
        if (uomId.HasValue && !await _uoms.ExistsAsync(orgId, uomId.Value, ct))
            throw new ConflictException("Unit of measure does not belong to the organization.");
    }

    private static ItemResponse Map(Item i) => new()
    {
        Id = i.Id,
        Code = i.Code,
        Name = i.Name,
        Description = i.Description,
        ItemType = i.ItemType,
        CategoryId = i.CategoryId,
        BaseUomId = i.BaseUomId,
        Barcode = i.Barcode,
        TrackBatches = i.TrackBatches,
        TrackSerials = i.TrackSerials,
        TrackExpiry = i.TrackExpiry,
        StandardCost = i.StandardCost,
        ReorderLevel = i.ReorderLevel,
        SafetyStock = i.SafetyStock,
        MinStock = i.MinStock,
        MaxStock = i.MaxStock,
        ReorderQty = i.ReorderQty,
        IsPurchasable = i.IsPurchasable,
        IsManufactured = i.IsManufactured,
        IsSellable = i.IsSellable,
        IsActive = i.IsActive,
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt,
        RowVersion = i.RowVersion is null ? string.Empty : Convert.ToBase64String(i.RowVersion)
    };
}

public interface IWarehouseService
{
    Task<WarehouseResponse> CreateAsync(CreateWarehouseRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<WarehouseResponse>> ListAsync(CancellationToken ct = default);
    Task<WarehouseResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<WarehouseResponse> UpdateAsync(Guid id, UpdateWarehouseRequest request, CancellationToken ct = default);
}

public sealed class WarehouseService : IWarehouseService
{
    private readonly IWarehouseRepository _warehouses;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;

    public WarehouseService(IWarehouseRepository warehouses, ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit, IClock clock)
    {
        _warehouses = warehouses;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _clock = clock;
    }

    public async Task<WarehouseResponse> CreateAsync(CreateWarehouseRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (await _warehouses.CodeExistsAsync(orgId, request.Code, null, ct))
            throw new DuplicateEntityException($"A warehouse with code '{request.Code}' already exists.");

        var wh = new Warehouse
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Code = request.Code,
            Name = request.Name,
            WarehouseType = string.IsNullOrEmpty(request.WarehouseType) ? WarehouseTypes.MainStore : request.WarehouseType,
            ProjectId = request.ProjectId,
            Address = request.Address,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _warehouses.AddAsync(wh, token);
            _audit.Add(EntityTypes.Warehouse, wh.Id, AuditActions.Create, null, new { wh.Code, wh.Name });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(wh);
    }

    public async Task<IReadOnlyList<WarehouseResponse>> ListAsync(CancellationToken ct = default)
    {
        var items = await _warehouses.ListAsync(_tenant.OrganizationId, ct);
        return items.Select(Map).ToList();
    }

    public async Task<WarehouseResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var wh = await _warehouses.GetAsync(_tenant.OrganizationId, id, false, ct) ?? throw NotFoundException.For("Warehouse", id);
        return Map(wh);
    }

    public async Task<WarehouseResponse> UpdateAsync(Guid id, UpdateWarehouseRequest request, CancellationToken ct = default)
    {
        var wh = await _warehouses.GetAsync(_tenant.OrganizationId, id, true, ct) ?? throw NotFoundException.For("Warehouse", id);
        wh.Name = request.Name;
        if (!string.IsNullOrEmpty(request.WarehouseType)) wh.WarehouseType = request.WarehouseType;
        wh.ProjectId = request.ProjectId;
        wh.Address = request.Address;
        wh.IsActive = request.IsActive;
        wh.UpdatedAt = _clock.UtcNow;
        wh.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.Warehouse, wh.Id, AuditActions.Update, null, new { wh.Name });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(wh);
    }

    private static WarehouseResponse Map(Warehouse w) => new()
    {
        Id = w.Id,
        Code = w.Code,
        Name = w.Name,
        WarehouseType = w.WarehouseType,
        ProjectId = w.ProjectId,
        Address = w.Address,
        IsActive = w.IsActive,
        CreatedAt = w.CreatedAt
    };
}

public interface ISupplierService
{
    Task<SupplierResponse> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default);
    Task<PagedResult<SupplierResponse>> ListAsync(SupplierFilter filter, CancellationToken ct = default);
    Task<SupplierResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<SupplierResponse> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken ct = default);
}

public sealed class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _suppliers;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;

    public SupplierService(ISupplierRepository suppliers, ITenantContext tenant, IUnitOfWork uow, IAuditWriter audit, IClock clock)
    {
        _suppliers = suppliers;
        _tenant = tenant;
        _uow = uow;
        _audit = audit;
        _clock = clock;
    }

    public async Task<SupplierResponse> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (await _suppliers.CodeExistsAsync(orgId, request.Code, null, ct))
            throw new DuplicateEntityException($"A supplier with code '{request.Code}' already exists.");

        var s = new Supplier
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Code = request.Code,
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            Status = "ACTIVE",
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _suppliers.AddAsync(s, token);
            _audit.Add(EntityTypes.Supplier, s.Id, AuditActions.Create, null, new { s.Code, s.Name });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(s);
    }

    public async Task<PagedResult<SupplierResponse>> ListAsync(SupplierFilter filter, CancellationToken ct = default)
    {
        var result = await _suppliers.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<SupplierResponse>
        {
            Items = result.Items.Select(Map).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<SupplierResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _suppliers.GetAsync(_tenant.OrganizationId, id, false, ct) ?? throw NotFoundException.For("Supplier", id);
        return Map(s);
    }

    public async Task<SupplierResponse> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken ct = default)
    {
        var s = await _suppliers.GetAsync(_tenant.OrganizationId, id, true, ct) ?? throw NotFoundException.For("Supplier", id);
        s.Name = request.Name;
        s.Email = request.Email;
        s.Phone = request.Phone;
        s.Address = request.Address;
        if (!string.IsNullOrEmpty(request.Status)) s.Status = request.Status;
        s.UpdatedAt = _clock.UtcNow;
        s.UpdatedBy = _tenant.UserId;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            _audit.Add(EntityTypes.Supplier, s.Id, AuditActions.Update, null, new { s.Name });
            await _uow.SaveChangesAsync(token);
        }, ct);

        return Map(s);
    }

    private static SupplierResponse Map(Supplier s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Name = s.Name,
        Email = s.Email,
        Phone = s.Phone,
        Address = s.Address,
        Status = s.Status,
        CreatedAt = s.CreatedAt
    };
}
