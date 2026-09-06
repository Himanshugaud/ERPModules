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

public sealed class GoodsReceiptsFunctions
{
    private readonly IGoodsReceiptService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateGoodsReceiptRequest> _validator;

    public GoodsReceiptsFunctions(IGoodsReceiptService service, IAuthorizationGuard auth, IValidator<CreateGoodsReceiptRequest> validator)
    {
        _service = service;
        _auth = auth;
        _validator = validator;
    }

    [Function("CreateGoodsReceipt")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/goods-receipts")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.GoodsReceiptCreate);
        var body = await Http.ReadValidatedAsync(req, _validator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("ListGoodsReceipts")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/goods-receipts")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.GoodsReceiptRead);
        return Http.Paged(await _service.ListAsync(DocFilter(req), ct));
    }

    [Function("GetGoodsReceipt")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/goods-receipts/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.GoodsReceiptRead);
        return Http.Ok(await _service.GetAsync(id, ct));
    }

    private static InventoryDocFilter DocFilter(HttpRequest req) => new()
    {
        Page = Http.IntQuery(req, "page") ?? 1,
        PageSize = Http.IntQuery(req, "pageSize") ?? 25,
        WarehouseId = Http.GuidQuery(req, "warehouseId"),
        SupplierId = Http.GuidQuery(req, "supplierId")
    };
}

public sealed class MaterialIssuesFunctions
{
    private readonly IMaterialIssueService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateMaterialIssueRequest> _validator;

    public MaterialIssuesFunctions(IMaterialIssueService service, IAuthorizationGuard auth, IValidator<CreateMaterialIssueRequest> validator)
    {
        _service = service;
        _auth = auth;
        _validator = validator;
    }

    [Function("CreateMaterialIssue")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/material-issues")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.IssueCreate);
        var body = await Http.ReadValidatedAsync(req, _validator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("ListMaterialIssues")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/material-issues")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.IssueRead);
        var filter = new InventoryDocFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            WarehouseId = Http.GuidQuery(req, "warehouseId"),
            ProjectId = Http.GuidQuery(req, "projectId")
        };
        return Http.Paged(await _service.ListAsync(filter, ct));
    }

    [Function("GetMaterialIssue")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/material-issues/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.IssueRead);
        return Http.Ok(await _service.GetAsync(id, ct));
    }
}

public sealed class StockTransfersFunctions
{
    private readonly IStockTransferService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateStockTransferRequest> _validator;

    public StockTransfersFunctions(IStockTransferService service, IAuthorizationGuard auth, IValidator<CreateStockTransferRequest> validator)
    {
        _service = service;
        _auth = auth;
        _validator = validator;
    }

    [Function("CreateStockTransfer")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/stock-transfers")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.TransferCreate);
        var body = await Http.ReadValidatedAsync(req, _validator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("ListStockTransfers")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/stock-transfers")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.TransferRead);
        var filter = new InventoryDocFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            WarehouseId = Http.GuidQuery(req, "warehouseId")
        };
        return Http.Paged(await _service.ListAsync(filter, ct));
    }

    [Function("GetStockTransfer")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/stock-transfers/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.TransferRead);
        return Http.Ok(await _service.GetAsync(id, ct));
    }
}

public sealed class StockAdjustmentsFunctions
{
    private readonly IStockAdjustmentService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateStockAdjustmentRequest> _validator;

    public StockAdjustmentsFunctions(IStockAdjustmentService service, IAuthorizationGuard auth, IValidator<CreateStockAdjustmentRequest> validator)
    {
        _service = service;
        _auth = auth;
        _validator = validator;
    }

    [Function("CreateStockAdjustment")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/stock-adjustments")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.StockAdjust);
        var body = await Http.ReadValidatedAsync(req, _validator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("ListStockAdjustments")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/stock-adjustments")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.StockRead);
        var filter = new InventoryDocFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            WarehouseId = Http.GuidQuery(req, "warehouseId")
        };
        return Http.Paged(await _service.ListAsync(filter, ct));
    }

    [Function("GetStockAdjustment")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/stock-adjustments/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.StockRead);
        return Http.Ok(await _service.GetAsync(id, ct));
    }
}
