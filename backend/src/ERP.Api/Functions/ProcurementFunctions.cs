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

public sealed class MaterialRequirementsFunctions
{
    private readonly IMaterialRequirementService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateMaterialRequirementRequest> _createValidator;
    private readonly IValidator<RejectMaterialRequirementRequest> _rejectValidator;
    private readonly IValidator<ConvertToPurchaseOrderRequest> _convertValidator;

    public MaterialRequirementsFunctions(IMaterialRequirementService service, IAuthorizationGuard auth,
        IValidator<CreateMaterialRequirementRequest> createValidator, IValidator<RejectMaterialRequirementRequest> rejectValidator,
        IValidator<ConvertToPurchaseOrderRequest> convertValidator)
    {
        _service = service;
        _auth = auth;
        _createValidator = createValidator;
        _rejectValidator = rejectValidator;
        _convertValidator = convertValidator;
    }

    [Function("CreateMaterialRequirement")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/material-requirements")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.MaterialRequirementCreate);
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("ListMaterialRequirements")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/material-requirements")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.MaterialRequirementRead);
        var filter = new MaterialRequirementFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            ProjectId = Http.GuidQuery(req, "projectId"),
            WarehouseId = Http.GuidQuery(req, "warehouseId"),
            Status = Http.StringQuery(req, "status"),
            Priority = Http.StringQuery(req, "priority")
        };
        return Http.Paged(await _service.ListAsync(filter, ct));
    }

    [Function("GetMaterialRequirement")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/material-requirements/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.MaterialRequirementRead);
        return Http.Ok(await _service.GetAsync(id, ct));
    }

    [Function("ApproveMaterialRequirement")]
    public async Task<IActionResult> Approve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/material-requirements/{id:guid}/approve")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.MaterialRequirementApprove);
        return Http.Ok(await _service.ApproveAsync(id, ct));
    }

    [Function("RejectMaterialRequirement")]
    public async Task<IActionResult> Reject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/material-requirements/{id:guid}/reject")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.MaterialRequirementApprove);
        var body = await Http.ReadValidatedAsync(req, _rejectValidator, ct);
        return Http.Ok(await _service.RejectAsync(id, body, ct));
    }

    [Function("ConvertMaterialRequirementToPurchaseOrder")]
    public async Task<IActionResult> Convert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/material-requirements/{id:guid}/convert-to-po")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.MaterialRequirementConvert);
        var body = await Http.ReadValidatedAsync(req, _convertValidator, ct);
        return Http.Created(await _service.ConvertToPurchaseOrderAsync(id, body, ct));
    }
}

public sealed class PurchaseOrdersFunctions
{
    private readonly IPurchaseOrderService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreatePurchaseOrderRequest> _createValidator;
    private readonly IValidator<UpdatePurchaseOrderRequest> _updateValidator;
    private readonly IValidator<RejectPurchaseOrderRequest> _rejectValidator;

    public PurchaseOrdersFunctions(IPurchaseOrderService service, IAuthorizationGuard auth,
        IValidator<CreatePurchaseOrderRequest> createValidator, IValidator<UpdatePurchaseOrderRequest> updateValidator,
        IValidator<RejectPurchaseOrderRequest> rejectValidator)
    {
        _service = service;
        _auth = auth;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _rejectValidator = rejectValidator;
    }

    [Function("CreatePurchaseOrder")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/purchase-orders")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.PurchaseOrderCreate);
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("ListPurchaseOrders")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/purchase-orders")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.PurchaseOrderRead);
        var filter = new PurchaseOrderFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            SupplierId = Http.GuidQuery(req, "supplierId"),
            ProjectId = Http.GuidQuery(req, "projectId"),
            Status = Http.StringQuery(req, "status")
        };
        return Http.Paged(await _service.ListAsync(filter, ct));
    }

    [Function("GetPurchaseOrder")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/purchase-orders/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.PurchaseOrderRead);
        return Http.Ok(await _service.GetAsync(id, ct));
    }

    [Function("UpdatePurchaseOrder")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/purchase-orders/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.PurchaseOrderUpdate);
        var body = await Http.ReadValidatedAsync(req, _updateValidator, ct);
        return Http.Ok(await _service.UpdateAsync(id, body, ct));
    }

    [Function("SubmitPurchaseOrder")]
    public async Task<IActionResult> Submit(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/purchase-orders/{id:guid}/submit")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.PurchaseOrderUpdate);
        return Http.Ok(await _service.SubmitAsync(id, ct));
    }

    [Function("ApprovePurchaseOrder")]
    public async Task<IActionResult> Approve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/purchase-orders/{id:guid}/approve")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.PurchaseOrderApprove);
        return Http.Ok(await _service.ApproveAsync(id, ct));
    }

    [Function("RejectPurchaseOrder")]
    public async Task<IActionResult> Reject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/purchase-orders/{id:guid}/reject")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.PurchaseOrderApprove);
        var body = await Http.ReadValidatedAsync(req, _rejectValidator, ct);
        return Http.Ok(await _service.RejectAsync(id, body, ct));
    }

    [Function("MarkPurchaseOrderOrdered")]
    public async Task<IActionResult> MarkOrdered(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/purchase-orders/{id:guid}/mark-ordered")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.PurchaseOrderUpdate);
        return Http.Ok(await _service.MarkOrderedAsync(id, ct));
    }

    [Function("ClosePurchaseOrder")]
    public async Task<IActionResult> Close(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/purchase-orders/{id:guid}/close")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.PurchaseOrderClose);
        return Http.Ok(await _service.CloseAsync(id, ct));
    }
}
