using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Application.Abstractions;
using ERP.Application.Inventory;
using ERP.Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class ItemsFunctions
{
    private readonly IItemService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateItemRequest> _createValidator;
    private readonly IValidator<UpdateItemRequest> _updateValidator;
    private readonly ReferenceCache _cache;
    private readonly ITenantContext _tenant;

    public ItemsFunctions(IItemService service, IAuthorizationGuard auth,
        IValidator<CreateItemRequest> createValidator, IValidator<UpdateItemRequest> updateValidator,
        ReferenceCache cache, ITenantContext tenant)
    {
        _service = service;
        _auth = auth;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _cache = cache;
        _tenant = tenant;
    }

    // One query, no paging: lets screens resolve item names without walking every page.
    [Function("ListItemLookup")]
    public async Task<IActionResult> Lookup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/items/lookup")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.ItemRead);
        return Http.Ok(await _cache.GetAsync(_tenant.OrganizationId, CacheGroups.Items, "lookup", TimeSpan.FromSeconds(60), () => _service.ListLookupAsync(ct)));
    }

    [Function("CreateItem")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/items")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.ItemCreate);
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        var created = await _service.CreateAsync(body, ct);
        _cache.Invalidate(_tenant.OrganizationId, CacheGroups.Items);
        return Http.Created(created);
    }

    [Function("ListItems")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/items")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.ItemRead);
        var filter = new ItemFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            Sort = Http.StringQuery(req, "sort"),
            ItemType = Http.StringQuery(req, "itemType"),
            CategoryId = Http.GuidQuery(req, "categoryId"),
            Search = Http.StringQuery(req, "search"),
            LowStock = bool.TryParse(Http.StringQuery(req, "lowStock"), out var low) ? low : null,
            Manufactured = bool.TryParse(Http.StringQuery(req, "manufactured"), out var mfg) ? mfg : null
        };
        return Http.Paged(await _service.ListAsync(filter, ct));
    }

    [Function("GetItem")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/items/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.ItemRead);
        return Http.Ok(await _service.GetAsync(id, ct));
    }

    [Function("UpdateItem")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/items/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.ItemUpdate);
        var body = await Http.ReadValidatedAsync(req, _updateValidator, ct);
        var updated = await _service.UpdateAsync(id, body, ct);
        _cache.Invalidate(_tenant.OrganizationId, CacheGroups.Items);
        return Http.Ok(updated);
    }

    [Function("DeleteItem")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/items/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.ItemDelete);
        await _service.DeleteAsync(id, ct);
        _cache.Invalidate(_tenant.OrganizationId, CacheGroups.Items);
        return Http.NoContent();
    }
}

public sealed class WarehousesFunctions
{
    private readonly IWarehouseService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateWarehouseRequest> _createValidator;
    private readonly IValidator<UpdateWarehouseRequest> _updateValidator;
    private readonly ReferenceCache _cache;
    private readonly ITenantContext _tenant;

    public WarehousesFunctions(IWarehouseService service, IAuthorizationGuard auth,
        IValidator<CreateWarehouseRequest> createValidator, IValidator<UpdateWarehouseRequest> updateValidator,
        ReferenceCache cache, ITenantContext tenant)
    {
        _service = service;
        _auth = auth;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _cache = cache;
        _tenant = tenant;
    }

    [Function("CreateWarehouse")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/warehouses")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.WarehouseCreate);
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        var created = await _service.CreateAsync(body, ct);
        _cache.Invalidate(_tenant.OrganizationId, CacheGroups.Warehouses);
        return Http.Created(created);
    }

    [Function("ListWarehouses")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/warehouses")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.WarehouseRead);
        return Http.Ok(await _cache.GetAsync(_tenant.OrganizationId, CacheGroups.Warehouses, "list", TimeSpan.FromMinutes(5), () => _service.ListAsync(ct)));
    }

    [Function("GetWarehouse")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/warehouses/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.WarehouseRead);
        return Http.Ok(await _service.GetAsync(id, ct));
    }

    [Function("UpdateWarehouse")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/warehouses/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.WarehouseUpdate);
        var body = await Http.ReadValidatedAsync(req, _updateValidator, ct);
        var updated = await _service.UpdateAsync(id, body, ct);
        _cache.Invalidate(_tenant.OrganizationId, CacheGroups.Warehouses);
        return Http.Ok(updated);
    }
}

public sealed class SuppliersFunctions
{
    private readonly ISupplierService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateSupplierRequest> _createValidator;
    private readonly IValidator<UpdateSupplierRequest> _updateValidator;

    public SuppliersFunctions(ISupplierService service, IAuthorizationGuard auth,
        IValidator<CreateSupplierRequest> createValidator, IValidator<UpdateSupplierRequest> updateValidator)
    {
        _service = service;
        _auth = auth;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [Function("CreateSupplier")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/suppliers")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.SupplierCreate);
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("ListSuppliers")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/suppliers")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.SupplierRead);
        var filter = new SupplierFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            Search = Http.StringQuery(req, "search"),
            Status = Http.StringQuery(req, "status")
        };
        return Http.Paged(await _service.ListAsync(filter, ct));
    }

    [Function("GetSupplier")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/suppliers/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.SupplierRead);
        return Http.Ok(await _service.GetAsync(id, ct));
    }

    [Function("UpdateSupplier")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/suppliers/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.SupplierUpdate);
        var body = await Http.ReadValidatedAsync(req, _updateValidator, ct);
        return Http.Ok(await _service.UpdateAsync(id, body, ct));
    }
}

public sealed class InventoryLookupFunctions
{
    private readonly IInventoryLookupService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateItemCategoryRequest> _createCategoryValidator;
    private readonly ReferenceCache _cache;
    private readonly ITenantContext _tenant;

    public InventoryLookupFunctions(IInventoryLookupService service, IAuthorizationGuard auth,
        IValidator<CreateItemCategoryRequest> createCategoryValidator, ReferenceCache cache, ITenantContext tenant)
    {
        _service = service;
        _auth = auth;
        _createCategoryValidator = createCategoryValidator;
        _cache = cache;
        _tenant = tenant;
    }

    [Function("ListItemCategories")]
    public async Task<IActionResult> Categories(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/item-categories")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.ItemRead);
        return Http.Ok(await _cache.GetAsync(_tenant.OrganizationId, CacheGroups.Items, "categories", TimeSpan.FromSeconds(60), () => _service.ListCategoriesAsync(ct)));
    }

    [Function("CreateItemCategory")]
    public async Task<IActionResult> CreateCategory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/item-categories")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.ItemCreate);
        var body = await Http.ReadValidatedAsync(req, _createCategoryValidator, ct);
        var created = await _service.CreateCategoryAsync(body, ct);
        _cache.Invalidate(_tenant.OrganizationId, CacheGroups.Items);
        return Http.Created(created);
    }

    [Function("ListUnitsOfMeasure")]
    public async Task<IActionResult> Uoms(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/units-of-measure")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.ItemRead);
        return Http.Ok(await _cache.GetAsync(_tenant.OrganizationId, CacheGroups.Lookups, "uoms", TimeSpan.FromMinutes(10), () => _service.ListUomsAsync(ct)));
    }
}
