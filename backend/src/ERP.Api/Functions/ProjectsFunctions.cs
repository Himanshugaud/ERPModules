using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Application.Abstractions;
using ERP.Application.Projects;
using ERP.Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class ProjectsFunctions
{
    private readonly IProjectService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateProjectRequest> _createValidator;
    private readonly IValidator<UpdateProjectRequest> _updateValidator;

    public ProjectsFunctions(
        IProjectService service,
        IAuthorizationGuard auth,
        IValidator<CreateProjectRequest> createValidator,
        IValidator<UpdateProjectRequest> updateValidator)
    {
        _service = service;
        _auth = auth;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [Function("CreateProject")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/projects")] HttpRequest req,
        CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectCreate);
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        var created = await _service.CreateAsync(body, ct);
        return Http.Created(created);
    }

    [Function("ListProjects")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects")] HttpRequest req,
        CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectRead);
        var filter = new ProjectFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            Sort = Http.StringQuery(req, "sort"),
            StatusId = Http.GuidQuery(req, "statusId"),
            PriorityId = Http.GuidQuery(req, "priorityId"),
            ManagerId = Http.GuidQuery(req, "managerId"),
            ClientId = Http.GuidQuery(req, "clientId"),
            StartDateFrom = Http.DateQuery(req, "startDateFrom"),
            StartDateTo = Http.DateQuery(req, "startDateTo"),
            Search = Http.StringQuery(req, "search")
        };
        var result = await _service.ListAsync(filter, ct);
        return Http.Paged(result);
    }

    [Function("GetProject")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}")] HttpRequest req,
        Guid projectId,
        CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectRead);
        var project = await _service.GetAsync(projectId, ct);
        return Http.Ok(project);
    }

    [Function("UpdateProject")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/projects/{projectId:guid}")] HttpRequest req,
        Guid projectId,
        CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectUpdate);
        var body = await Http.ReadValidatedAsync(req, _updateValidator, ct);
        var updated = await _service.UpdateAsync(projectId, body, ct);
        return Http.Ok(updated);
    }

    [Function("DeleteProject")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/projects/{projectId:guid}")] HttpRequest req,
        Guid projectId,
        CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectDelete);
        await _service.DeleteAsync(projectId, ct);
        return Http.NoContent();
    }
}
