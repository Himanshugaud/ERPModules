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

public sealed class BomsFunctions
{
    private readonly IBomService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateBomRequest> _validator;

    public BomsFunctions(IBomService service, IAuthorizationGuard auth, IValidator<CreateBomRequest> validator)
    {
        _service = service;
        _auth = auth;
        _validator = validator;
    }

    [Function("CreateBom")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/boms")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.BomCreate);
        var body = await Http.ReadValidatedAsync(req, _validator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("ListBoms")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/boms")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.BomRead);
        var filter = new BomFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            OutputItemId = Http.GuidQuery(req, "outputItemId"),
            Search = Http.StringQuery(req, "search")
        };
        return Http.Paged(await _service.ListAsync(filter, ct));
    }

    [Function("GetBom")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/boms/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.BomRead);
        return Http.Ok(await _service.GetAsync(id, ct));
    }
}

public sealed class WorkOrdersFunctions
{
    private readonly IWorkOrderService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateWorkOrderRequest> _createValidator;
    private readonly IValidator<CompleteWorkOrderRequest> _completeValidator;

    public WorkOrdersFunctions(IWorkOrderService service, IAuthorizationGuard auth,
        IValidator<CreateWorkOrderRequest> createValidator, IValidator<CompleteWorkOrderRequest> completeValidator)
    {
        _service = service;
        _auth = auth;
        _createValidator = createValidator;
        _completeValidator = completeValidator;
    }

    [Function("CreateWorkOrder")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/work-orders")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.WorkOrderCreate);
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("ListWorkOrders")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/work-orders")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.WorkOrderRead);
        var filter = new WorkOrderFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            Status = Http.StringQuery(req, "status"),
            ProjectId = Http.GuidQuery(req, "projectId"),
            WarehouseId = Http.GuidQuery(req, "warehouseId")
        };
        return Http.Paged(await _service.ListAsync(filter, ct));
    }

    [Function("GetWorkOrder")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/work-orders/{id:guid}")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.WorkOrderRead);
        return Http.Ok(await _service.GetAsync(id, ct));
    }

    [Function("ReleaseWorkOrder")]
    public async Task<IActionResult> Release(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/work-orders/{id:guid}/release")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.WorkOrderRelease);
        return Http.Ok(await _service.ReleaseAsync(id, ct));
    }

    [Function("CompleteWorkOrder")]
    public async Task<IActionResult> Complete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/work-orders/{id:guid}/complete")] HttpRequest req, Guid id, CancellationToken ct)
    {
        _auth.Require(Permissions.WorkOrderComplete);
        var body = await Http.ReadValidatedAsync(req, _completeValidator, ct);
        return Http.Ok(await _service.CompleteAsync(id, body, ct));
    }
}
