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

        var user = await _users.GetByEmailAsync(org.Id, request.Email, ct);
        if (user is null || user.Status != "ACTIVE")
            throw new UnauthorizedException("Invalid organization or credentials.");

        var roles = await _users.GetRoleNamesAsync(user.Id, ct);
        var permissions = await _users.GetPermissionCodesAsync(user.Id, ct);

        var (token, expiresAt) = _jwt.Generate(user.Id, org.Id, org.Name, user.Email, user.DisplayName, roles, permissions);

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
                DisplayName = user.DisplayName,
                Roles = roles,
                Permissions = permissions
            }
        };
    }
}
