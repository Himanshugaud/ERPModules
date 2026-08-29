using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class LookupsFunctions
{
    private readonly IProjectStatusRepository _statuses;
    private readonly IProjectPriorityRepository _priorities;
    private readonly IClientRepository _clients;
    private readonly IDepartmentRepository _departments;
    private readonly ITenantContext _tenant;
    private readonly IAuthorizationGuard _auth;

    public LookupsFunctions(
        IProjectStatusRepository statuses,
        IProjectPriorityRepository priorities,
        IClientRepository clients,
        IDepartmentRepository departments,
        ITenantContext tenant,
        IAuthorizationGuard auth)
    {
        _statuses = statuses;
        _priorities = priorities;
        _clients = clients;
        _departments = departments;
        _tenant = tenant;
        _auth = auth;
    }

    [Function("ListProjectStatuses")]
    public async Task<IActionResult> Statuses(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/project-statuses")] HttpRequest req,
        CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectRead);
        var items = await _statuses.ListAsync(_tenant.OrganizationId, ct);
        return Http.Ok(items.Select(s => new { s.Id, s.Code, s.Name, s.DisplayOrder, s.IsDefault, s.IsFinal, s.IsActive }));
    }

    [Function("ListProjectPriorities")]
    public async Task<IActionResult> Priorities(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/project-priorities")] HttpRequest req,
        CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectRead);
        var items = await _priorities.ListAsync(_tenant.OrganizationId, ct);
        return Http.Ok(items.Select(p => new { p.Id, p.Code, p.Name, p.DisplayOrder, p.IsActive }));
    }

    [Function("ListDepartments")]
    public async Task<IActionResult> Departments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/departments")] HttpRequest req,
        CancellationToken ct)
    {
        _auth.RequireAuthenticated();
        var items = await _departments.ListAsync(_tenant.OrganizationId, ct);
        return Http.Ok(items.Select(d => new { d.Id, d.Code, d.Name }));
    }
}

public sealed class HealthFunction
{
    [Function("Health")]
    public IActionResult Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequest req)
        => new OkObjectResult(new { status = "ok", timestamp = DateTime.UtcNow });
}
