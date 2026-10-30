using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ERP.Application.Abstractions;
using ERP.Shared.Exceptions;

namespace ERP.Application.Identity;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public sealed class AuthService : IAuthService
{
    private readonly IOrganizationRepository _orgs;
    private readonly IUserRepository _users;
    private readonly IJwtTokenService _jwt;

    public AuthService(IOrganizationRepository orgs, IUserRepository users, IJwtTokenService jwt)
    {
        _orgs = orgs;
        _users = users;
        _jwt = jwt;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var org = await _orgs.GetByCodeAsync(request.OrganizationCode, ct);
        if (org is null || org.Status != "ACTIVE")
            throw new UnauthorizedException("Invalid organization or credentials.");

        var user = request.Email.Contains('@')
            ? await _users.GetByEmailAsync(org.Id, request.Email, ct)
            : await _users.GetByUsernameAsync(org.Id, request.Email, ct);
        if (user is null || user.Status != "ACTIVE")
            throw new UnauthorizedException("Invalid organization or credentials.");
        if (string.IsNullOrEmpty(user.PasswordHash) ||
            string.IsNullOrEmpty(request.Password) || !PasswordSecurity.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid organization or credentials.");

        var roles = await _users.GetRoleNamesAsync(user.Id, ct);
        var permissions = await _users.GetPermissionCodesAsync(user.Id, ct);

        var (token, expiresAt) = _jwt.Generate(user.Id, org.Id, org.Name, user.Email, user.DisplayName, roles, permissions,
            UserSessionValidator.GetCredentialVersion(user.PasswordHash));

        return new LoginResponse
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            User = new MeResponse
            {
                UserId = user.Id,
                OrganizationId = org.Id,
                OrganizationName = org.Name,
                Email = user.Email,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Roles = roles,
                Permissions = permissions
            }
        };
    }
}

public interface IUserSessionValidator
{
    Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken ct = default);
}

public sealed class UserSessionValidator : IUserSessionValidator
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUserRepository _users;

    public UserSessionValidator(IOrganizationRepository organizations, IUserRepository users)
    {
        _organizations = organizations;
        _users = users;
    }

    public static string GetCredentialVersion(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));

    public async Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(principal.FindFirst("sub")?.Value, out var userId) ||
            !Guid.TryParse(principal.FindFirst("organizationId")?.Value, out var organizationId))
            return false;

        var organization = await _organizations.GetAsync(organizationId, ct);
        if (organization is null || organization.Status != "ACTIVE") return false;
        var user = await _users.GetAsync(organizationId, userId, false, ct);
        if (user is null || user.OrganizationId != organizationId || user.Status != "ACTIVE" ||
            string.IsNullOrEmpty(user.PasswordHash) ||
            principal.FindFirst("credentialVersion")?.Value != GetCredentialVersion(user.PasswordHash))
            return false;

        var roles = await _users.GetRoleNamesAsync(userId, ct);
        var permissions = await _users.GetPermissionCodesAsync(userId, ct);
        return new HashSet<string>(roles, StringComparer.Ordinal).SetEquals(principal.FindAll("roles").Select(claim => claim.Value)) &&
            new HashSet<string>(permissions, StringComparer.Ordinal).SetEquals(principal.FindAll("permissions").Select(claim => claim.Value));
    }
}
