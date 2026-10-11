using ERP.Application.Abstractions;
using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class RequirementSheetsFunctions
{
    private readonly IRequirementSheetRepository _sheets;
    private readonly ITenantContext _tenant;
    private readonly IAuthorizationGuard _auth;

    public RequirementSheetsFunctions(IRequirementSheetRepository sheets, ITenantContext tenant, IAuthorizationGuard auth)
    {
        _sheets = sheets;
        _tenant = tenant;
        _auth = auth;
    }

    // Sheet types -> sections -> items, used to build (and export) requirement sheets in one round trip.
    [Function("GetRequirementSheetCatalog")]
    public async Task<IActionResult> Catalog(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/requirement-sheets/catalog")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.MaterialRequirementRead);
        return Http.Ok(await _sheets.GetCatalogAsync(_tenant.OrganizationId, ct));
    }
}
