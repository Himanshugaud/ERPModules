using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Application.Projects;
using ERP.Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class ProjectMembersFunctions
{
    private readonly IProjectMemberService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<AddProjectMemberRequest> _addValidator;
    private readonly IValidator<UpdateProjectMemberRequest> _updateValidator;

    public ProjectMembersFunctions(IProjectMemberService service, IAuthorizationGuard auth,
        IValidator<AddProjectMemberRequest> addValidator, IValidator<UpdateProjectMemberRequest> updateValidator)
    {
        _service = service;
        _auth = auth;
        _addValidator = addValidator;
        _updateValidator = updateValidator;
    }

    [Function("ListProjectMembers")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/projects/{projectId:guid}/members")] HttpRequest req,
        Guid projectId, CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectRead);
        return Http.Ok(await _service.ListAsync(projectId, ct));
    }

    [Function("AddProjectMember")]
    public async Task<IActionResult> Add(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/projects/{projectId:guid}/members")] HttpRequest req,
        Guid projectId, CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectUpdate);
        var body = await Http.ReadValidatedAsync(req, _addValidator, ct);
        return Http.Created(await _service.AddAsync(projectId, body, ct));
    }

    [Function("UpdateProjectMember")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/projects/{projectId:guid}/members/{userId:guid}")] HttpRequest req,
        Guid projectId, Guid userId, CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectUpdate);
        var body = await Http.ReadValidatedAsync(req, _updateValidator, ct);
        return Http.Ok(await _service.UpdateAsync(projectId, userId, body, ct));
    }

    [Function("RemoveProjectMember")]
    public async Task<IActionResult> Remove(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/projects/{projectId:guid}/members/{userId:guid}")] HttpRequest req,
        Guid projectId, Guid userId, CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectUpdate);
        await _service.RemoveAsync(projectId, userId, ct);
        return Http.NoContent();
    }
}
