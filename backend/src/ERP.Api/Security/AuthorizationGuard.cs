using ERP.Application.Abstractions;
using ERP.Shared.Exceptions;

namespace ERP.Api.Security;

public interface IAuthorizationGuard
{
    void Require(string permission);
    void RequireAnyRole(params string[] roles);
}

public sealed class AuthorizationGuard : IAuthorizationGuard
{
    private readonly ICurrentUser _user;
    public AuthorizationGuard(ICurrentUser user) => _user = user;

    public void Require(string permission)
    {
        if (!_user.IsAuthenticated)
            throw new UnauthorizedException();
        if (!_user.HasPermission(permission))
            throw new ForbiddenException($"Missing required permission: {permission}");
    }

    public void RequireAnyRole(params string[] roles)
    {
        if (!_user.IsAuthenticated)
            throw new UnauthorizedException();
        if (!roles.Any(r => _user.Roles.Contains(r)))
            throw new ForbiddenException($"Requires one of roles: {string.Join(", ", roles)}");
    }
}
