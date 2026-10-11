using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Application.Planning;
using ERP.Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class ProjectPlanFunctions
{
    private readonly IProjectPlanService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<SavePlanEntryRequest> _entryValidator;
    private readonly IValidator<PublishPlanRequest> _publishValidator;

    public ProjectPlanFunctions(IProjectPlanService service, IAuthorizationGuard auth,
        IValidator<SavePlanEntryRequest> entryValidator, IValidator<PublishPlanRequest> publishValidator)
    {
        _service = service;
        _auth = auth;
        _entryValidator = entryValidator;
        _publishValidator = publishValidator;
    }

    [Function("GetProjectPlan")]
    public async Task<IActionResult> GetPlan(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/plan")] HttpRequest req,
        Guid projectId, CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectRead);
        return Http.Ok(await _service.GetPlanAsync(projectId, ct));
    }

    [Function("PublishProjectPlan")]
    public async Task<IActionResult> Publish(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/projects/{projectId:guid}/plan/publish")] HttpRequest req,
        Guid projectId, CancellationToken ct)
    {
        _auth.Require(Permissions.PlanManage);
        var body = req.ContentLength is > 0 ? await Http.ReadValidatedAsync(req, _publishValidator, ct) : new PublishPlanRequest();
        return Http.Ok(await _service.PublishAsync(projectId, body, ct));
    }

    [Function("ListPlanEntries")]
    public async Task<IActionResult> ListEntries(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/plan/entries")] HttpRequest req,
        Guid projectId, CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectRead);
        return Http.Ok(await _service.ListEntriesAsync(projectId, Http.DateQuery(req, "from"), Http.DateQuery(req, "to"), ct));
    }

    [Function("CreatePlanEntry")]
    public async Task<IActionResult> CreateEntry(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/projects/{projectId:guid}/plan/entries")] HttpRequest req,
        Guid projectId, CancellationToken ct)
    {
        _auth.Require(Permissions.PlanManage);
        var body = await Http.ReadValidatedAsync(req, _entryValidator, ct);
        return Http.Created(await _service.CreateEntryAsync(projectId, body, ct));
    }

    [Function("UpdatePlanEntry")]
    public async Task<IActionResult> UpdateEntry(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/plan/entries/{entryId:guid}")] HttpRequest req,
        Guid entryId, CancellationToken ct)
    {
        _auth.Require(Permissions.PlanManage);
        var body = await Http.ReadValidatedAsync(req, _entryValidator, ct);
        return Http.Ok(await _service.UpdateEntryAsync(entryId, body, ct));
    }

    [Function("DeletePlanEntry")]
    public async Task<IActionResult> DeleteEntry(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/plan/entries/{entryId:guid}")] HttpRequest req,
        Guid entryId, CancellationToken ct)
    {
        _auth.Require(Permissions.PlanManage);
        await _service.DeleteEntryAsync(entryId, ct);
        return Http.NoContent();
    }
}
