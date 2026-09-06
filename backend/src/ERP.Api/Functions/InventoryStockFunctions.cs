using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Application.Abstractions;
using ERP.Application.Inventory;
using ERP.Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class StockFunctions
{
    private readonly IInventoryLookupService _service;
    private readonly IAuthorizationGuard _auth;

    public StockFunctions(IInventoryLookupService service, IAuthorizationGuard auth)
    {
        _service = service;
        _auth = auth;
    }

    [Function("ListStock")]
    public async Task<IActionResult> ListStock(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/stock")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.StockRead);
        var filter = new StockLevelFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            ItemId = Http.GuidQuery(req, "itemId"),
            WarehouseId = Http.GuidQuery(req, "warehouseId"),
            BelowReorder = bool.TryParse(Http.StringQuery(req, "belowReorder"), out var br) ? br : null
        };
        return Http.Paged(await _service.ListStockAsync(filter, ct));
    }

    [Function("ListLowStock")]
    public async Task<IActionResult> ListLowStock(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/stock/low")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.StockRead);
        var filter = new StockLevelFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            WarehouseId = Http.GuidQuery(req, "warehouseId"),
            BelowReorder = true
        };
        return Http.Paged(await _service.ListStockAsync(filter, ct));
    }

    [Function("ListStockMovements")]
    public async Task<IActionResult> ListMovements(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/stock/movements")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.StockRead);
        var filter = new StockMovementFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 50,
            ItemId = Http.GuidQuery(req, "itemId"),
            WarehouseId = Http.GuidQuery(req, "warehouseId"),
            ProjectId = Http.GuidQuery(req, "projectId"),
            MovementType = Http.StringQuery(req, "movementType"),
            DateFrom = Http.DateQuery(req, "dateFrom"),
            DateTo = Http.DateQuery(req, "dateTo")
        };
        return Http.Paged(await _service.ListMovementsAsync(filter, ct));
    }
}
