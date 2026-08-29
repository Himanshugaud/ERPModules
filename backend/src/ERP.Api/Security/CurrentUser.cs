using System.Security.Claims;
using ERP.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ERP.Api.Security;

/// Resolves identity from the validated JWT (Entra ID) claims, with an optional
/// development header fallback (X-User-Id / X-Organization-Id / X-Permissions).
public sealed class CurrentUser : ICurrentUser
{
    private readonly bool _authenticated;
    public bool IsAuthenticated => _authenticated;
    public Guid UserId { get; }
    public Guid OrganizationId { get; }
    public string? OrganizationName { get; }
    public string? Email { get; }
    public string? DisplayName { get; }
    public IReadOnlyCollection<string> Roles { get; }
    public IReadOnlyCollection<string> Permissions { get; }

    public bool HasPermission(string permission) => Permissions.Contains(permission);

    public CurrentUser(IHttpContextAccessor accessor, IConfiguration config)
    {
        var http = accessor.HttpContext;
        var devEnabled = string.Equals(config["ERP_DevAuthEnabled"], "true", StringComparison.OrdinalIgnoreCase);

        if (http is null)
        {
            Roles = Array.Empty<string>();
            Permissions = Array.Empty<string>();
            return;
        }

        var principal = http.User;
        if (principal?.Identity?.IsAuthenticated == true)
        {
            UserId = ParseGuid(principal.FindFirst("sub")?.Value
                ?? principal.FindFirst("oid")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            OrganizationId = ParseGuid(principal.FindFirst("organizationId")?.Value ?? principal.FindFirst("tid")?.Value);
            OrganizationName = principal.FindFirst("orgName")?.Value;
            Email = principal.FindFirst("email")?.Value ?? principal.FindFirst("preferred_username")?.Value ?? principal.FindFirst(ClaimTypes.Email)?.Value;
            DisplayName = principal.FindFirst("name")?.Value;
            Roles = principal.FindAll("roles").Select(c => c.Value).ToArray();
            Permissions = principal.FindAll("permissions").Select(c => c.Value).ToArray();
            _authenticated = UserId != Guid.Empty && OrganizationId != Guid.Empty;
            return;
        }

        if (devEnabled &&
            http.Request.Headers.TryGetValue("X-User-Id", out var uid) &&
            http.Request.Headers.TryGetValue("X-Organization-Id", out var oid))
        {
            UserId = ParseGuid(uid);
            OrganizationId = ParseGuid(oid);
            Email = http.Request.Headers.TryGetValue("X-Email", out var email) ? email.ToString() : null;
            var perms = http.Request.Headers.TryGetValue("X-Permissions", out var p) ? p.ToString() : string.Empty;
            Permissions = perms.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Roles = Array.Empty<string>();
            _authenticated = UserId != Guid.Empty && OrganizationId != Guid.Empty;
            return;
        }

        Roles = Array.Empty<string>();
        Permissions = Array.Empty<string>();
    }

    private static Guid ParseGuid(string? value) => Guid.TryParse(value, out var g) ? g : Guid.Empty;
}
