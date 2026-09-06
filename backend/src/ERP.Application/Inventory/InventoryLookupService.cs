using ERP.Application.Abstractions;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;
using ERP.Shared.Pagination;

namespace ERP.Application.Inventory;

public interface IInventoryLookupService
{
    Task<IReadOnlyList<ItemCategoryResponse>> ListCategoriesAsync(CancellationToken ct = default);
    Task<ItemCategoryResponse> CreateCategoryAsync(CreateItemCategoryRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<UomResponse>> ListUomsAsync(CancellationToken ct = default);
    Task<PagedResult<StockLevelResponse>> ListStockAsync(StockLevelFilter filter, CancellationToken ct = default);
    Task<PagedResult<StockMovementResponse>> ListMovementsAsync(StockMovementFilter filter, CancellationToken ct = default);
}

public sealed class InventoryLookupService : IInventoryLookupService
{
    private readonly IItemCategoryRepository _categories;
    private readonly IUnitOfMeasureRepository _uoms;
    private readonly IStockLevelRepository _levels;
    private readonly IStockMovementRepository _movements;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public InventoryLookupService(IItemCategoryRepository categories, IUnitOfMeasureRepository uoms,
        IStockLevelRepository levels, IStockMovementRepository movements, ITenantContext tenant, IUnitOfWork uow, IClock clock)
    {
        _categories = categories;
        _uoms = uoms;
        _levels = levels;
        _movements = movements;
        _tenant = tenant;
        _uow = uow;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ItemCategoryResponse>> ListCategoriesAsync(CancellationToken ct = default)
    {
        var items = await _categories.ListAsync(_tenant.OrganizationId, ct);
        return items.Select(c => new ItemCategoryResponse
        {
            Id = c.Id, Code = c.Code, Name = c.Name, ParentCategoryId = c.ParentCategoryId, IsActive = c.IsActive
        }).ToList();
    }

    public async Task<ItemCategoryResponse> CreateCategoryAsync(CreateItemCategoryRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (await _categories.CodeExistsAsync(orgId, request.Code, null, ct))
            throw new DuplicateEntityException($"A category with code '{request.Code}' already exists.");
        if (request.ParentCategoryId.HasValue && !await _categories.ExistsAsync(orgId, request.ParentCategoryId.Value, ct))
            throw new ConflictException("Parent category does not belong to the organization.");

        var cat = new ItemCategory
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            ParentCategoryId = request.ParentCategoryId,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _tenant.UserId
        };

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await _categories.AddAsync(cat, token);
            await _uow.SaveChangesAsync(token);
        }, ct);

        return new ItemCategoryResponse { Id = cat.Id, Code = cat.Code, Name = cat.Name, ParentCategoryId = cat.ParentCategoryId, IsActive = cat.IsActive };
    }

    public async Task<IReadOnlyList<UomResponse>> ListUomsAsync(CancellationToken ct = default)
    {
        var items = await _uoms.ListAsync(_tenant.OrganizationId, ct);
        return items.Select(u => new UomResponse { Id = u.Id, Code = u.Code, Name = u.Name, IsBaseUnit = u.IsBaseUnit }).ToList();
    }

    public async Task<PagedResult<StockLevelResponse>> ListStockAsync(StockLevelFilter filter, CancellationToken ct = default)
    {
        var result = await _levels.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<StockLevelResponse>
        {
            Items = result.Items.Select(Map).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<PagedResult<StockMovementResponse>> ListMovementsAsync(StockMovementFilter filter, CancellationToken ct = default)
    {
        var result = await _movements.ListAsync(_tenant.OrganizationId, filter, ct);
        return new PagedResult<StockMovementResponse>
        {
            Items = result.Items.Select(m => new StockMovementResponse
            {
                Id = m.Id, ItemId = m.ItemId, WarehouseId = m.WarehouseId, BatchId = m.BatchId,
                MovementType = m.MovementType, Direction = m.Direction, Qty = m.Qty, UnitCost = m.UnitCost,
                TotalCost = m.TotalCost, RefDocType = m.RefDocType, RefDocId = m.RefDocId, ProjectId = m.ProjectId,
                OccurredAt = m.OccurredAt
            }).ToList(),
            TotalItems = result.TotalItems,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    private static StockLevelResponse Map(StockLevel s) => new()
    {
        Id = s.Id,
        ItemId = s.ItemId,
        WarehouseId = s.WarehouseId,
        BatchId = s.BatchId,
        QtyOnHand = s.QtyOnHand,
        QtyReserved = s.QtyReserved,
        QtyAvailable = s.QtyOnHand - s.QtyReserved,
        QtyInTransit = s.QtyInTransit,
        AvgUnitCost = s.AvgUnitCost,
        StockValue = s.QtyOnHand * s.AvgUnitCost
    };
}
