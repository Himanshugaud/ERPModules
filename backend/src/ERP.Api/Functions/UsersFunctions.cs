using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Application.Abstractions;
using ERP.Application.Identity;
using ERP.Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class UsersFunctions
{
    private readonly IUserService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateUserRequest> _createValidator;
    private readonly IValidator<UpdateUserRequest> _updateValidator;

    public UsersFunctions(IUserService service, IAuthorizationGuard auth,
        IValidator<CreateUserRequest> createValidator, IValidator<UpdateUserRequest> updateValidator)
    {
        _service = service;
        _auth = auth;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [Function("ListUsers")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/users")] HttpRequest req, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        var filter = new UserFilter
        {
            Page = Http.IntQuery(req, "page") ?? 1,
            PageSize = Http.IntQuery(req, "pageSize") ?? 25,
            Search = Http.StringQuery(req, "search"),
            Status = Http.StringQuery(req, "status"),
            DepartmentId = Http.GuidQuery(req, "departmentId")
        };
        return Http.Paged(await _service.ListAsync(filter, ct));
    }

    [Function("CreateUser")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/users")] HttpRequest req, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("GetUser")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/users/{userId:guid}")] HttpRequest req,
        Guid userId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        return Http.Ok(await _service.GetAsync(userId, ct));
    }

    [Function("UpdateUser")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/users/{userId:guid}")] HttpRequest req,
        Guid userId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        var body = await Http.ReadValidatedAsync(req, _updateValidator, ct);
        return Http.Ok(await _service.UpdateAsync(userId, body, ct));
    }

    [Function("DeleteUser")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/users/{userId:guid}")] HttpRequest req,
        Guid userId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        await _service.DeactivateAsync(userId, ct);
        return Http.NoContent();
    }

    [Function("GetUserRoles")]
    public async Task<IActionResult> GetRoles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/users/{userId:guid}/roles")] HttpRequest req,
        Guid userId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        return Http.Ok(await _service.GetRolesAsync(userId, ct));
    }

    [Function("AssignUserRole")]
    public async Task<IActionResult> AssignRole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/users/{userId:guid}/roles/{roleId:guid}")] HttpRequest req,
        Guid userId, Guid roleId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        await _service.AssignRoleAsync(userId, roleId, ct);
        return Http.NoContent();
    }

    [Function("RemoveUserRole")]
    public async Task<IActionResult> RemoveRole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/users/{userId:guid}/roles/{roleId:guid}")] HttpRequest req,
        Guid userId, Guid roleId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        await _service.RemoveRoleAsync(userId, roleId, ct);
        return Http.NoContent();
    }
}
