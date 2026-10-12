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
    private readonly ReferenceCache _cache;

    public LookupsFunctions(
        IProjectStatusRepository statuses,
        IProjectPriorityRepository priorities,
        IClientRepository clients,
        IDepartmentRepository departments,
        ITenantContext tenant,
        IAuthorizationGuard auth,
        ReferenceCache cache)
    {
        _statuses = statuses;
        _priorities = priorities;
        _clients = clients;
        _departments = departments;
        _tenant = tenant;
        _auth = auth;
        _cache = cache;
    }

    [Function("ListProjectStatuses")]
    public async Task<IActionResult> Statuses(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/project-statuses")] HttpRequest req,
        CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectRead);
        return Http.Ok(await _cache.GetAsync(_tenant.OrganizationId, CacheGroups.Lookups, "project-statuses", TimeSpan.FromMinutes(10), async () =>
            (await _statuses.ListAsync(_tenant.OrganizationId, ct))
                .Select(s => new { s.Id, s.Code, s.Name, s.DisplayOrder, s.IsDefault, s.IsFinal, s.IsActive }).ToList()));
    }

    [Function("ListProjectPriorities")]
    public async Task<IActionResult> Priorities(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/project-priorities")] HttpRequest req,
        CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectRead);
        return Http.Ok(await _cache.GetAsync(_tenant.OrganizationId, CacheGroups.Lookups, "project-priorities", TimeSpan.FromMinutes(10), async () =>
            (await _priorities.ListAsync(_tenant.OrganizationId, ct))
                .Select(p => new { p.Id, p.Code, p.Name, p.DisplayOrder, p.IsActive }).ToList()));
    }

    [Function("ListDepartments")]
    public async Task<IActionResult> Departments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/departments")] HttpRequest req,
        CancellationToken ct)
    {
        _auth.RequireAuthenticated();
        return Http.Ok(await _cache.GetAsync(_tenant.OrganizationId, CacheGroups.Lookups, "departments", TimeSpan.FromMinutes(10), async () =>
            (await _departments.ListAsync(_tenant.OrganizationId, ct)).Select(d => new { d.Id, d.Code, d.Name }).ToList()));
    }
}

public sealed class HealthFunction
{
    [Function("Health")]
    public IActionResult Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequest req)
        => new OkObjectResult(new { status = "ok", timestamp = DateTime.UtcNow });
}
