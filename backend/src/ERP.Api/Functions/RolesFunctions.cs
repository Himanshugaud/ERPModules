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

public sealed class RolesFunctions
{
    private readonly IRoleService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateRoleRequest> _createValidator;
    private readonly IValidator<UpdateRoleRequest> _updateValidator;

    public RolesFunctions(IRoleService service, IAuthorizationGuard auth,
        IValidator<CreateRoleRequest> createValidator, IValidator<UpdateRoleRequest> updateValidator)
    {
        _service = service;
        _auth = auth;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [Function("ListRoles")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/roles")] HttpRequest req, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        return Http.Ok(await _service.ListAsync(ct));
    }

    [Function("CreateRole")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/roles")] HttpRequest req, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("GetRole")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/roles/{roleId:guid}")] HttpRequest req,
        Guid roleId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        return Http.Ok(await _service.GetAsync(roleId, ct));
    }

    [Function("UpdateRole")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/roles/{roleId:guid}")] HttpRequest req,
        Guid roleId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        var body = await Http.ReadValidatedAsync(req, _updateValidator, ct);
        return Http.Ok(await _service.UpdateAsync(roleId, body, ct));
    }

    [Function("DeleteRole")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/roles/{roleId:guid}")] HttpRequest req,
        Guid roleId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        await _service.DeleteAsync(roleId, ct);
        return Http.NoContent();
    }

    [Function("GetRolePermissions")]
    public async Task<IActionResult> GetPermissions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/roles/{roleId:guid}/permissions")] HttpRequest req,
        Guid roleId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        return Http.Ok(await _service.GetPermissionsAsync(roleId, ct));
    }

    [Function("AssignRolePermission")]
    public async Task<IActionResult> AssignPermission(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/roles/{roleId:guid}/permissions/{permissionId:guid}")] HttpRequest req,
        Guid roleId, Guid permissionId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        await _service.AssignPermissionAsync(roleId, permissionId, ct);
        return Http.NoContent();
    }

    [Function("RemoveRolePermission")]
    public async Task<IActionResult> RemovePermission(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/roles/{roleId:guid}/permissions/{permissionId:guid}")] HttpRequest req,
        Guid roleId, Guid permissionId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        await _service.RemovePermissionAsync(roleId, permissionId, ct);
        return Http.NoContent();
    }
}

public sealed class PermissionsFunctions
{
    private readonly IPermissionRepository _permissions;
    private readonly IAuthorizationGuard _auth;

    public PermissionsFunctions(IPermissionRepository permissions, IAuthorizationGuard auth)
    {
        _permissions = permissions;
        _auth = auth;
    }

    [Function("ListPermissions")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/permissions")] HttpRequest req, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        var items = await _permissions.ListAsync(ct);
        return Http.Ok(items.Select(p => new { p.Id, p.Code, p.Name, p.Module, p.Resource, p.Action, p.IsActive }));
    }

    [Function("GetPermission")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/permissions/{permissionId:guid}")] HttpRequest req,
        Guid permissionId, CancellationToken ct)
    {
        _auth.RequireAnyRole(SystemRoles.Administrative);
        var p = await _permissions.GetAsync(permissionId, ct)
            ?? throw ERP.Shared.Exceptions.NotFoundException.For("Permission", permissionId);
        return Http.Ok(new { p.Id, p.Code, p.Name, p.Description, p.Module, p.Resource, p.Action, p.IsActive });
    }
}
