using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Application.Abstractions;
using ERP.Application.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class AuthFunctions
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthFunctions(IAuthService auth, ICurrentUser currentUser, IValidator<LoginRequest> loginValidator)
    {
        _auth = auth;
        _currentUser = currentUser;
        _loginValidator = loginValidator;
    }

    [Function("Login")]
    public async Task<IActionResult> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/login")] HttpRequest req,
        CancellationToken ct)
    {
        var body = await Http.ReadValidatedAsync(req, _loginValidator, ct);
        var response = await _auth.LoginAsync(body, ct);
        return Http.Ok(response);
    }

    [Function("Me")]
    public IActionResult Me(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/me")] HttpRequest req)
    {
        if (!_currentUser.IsAuthenticated)
            throw new ERP.Shared.Exceptions.UnauthorizedException();

        return Http.Ok(new MeResponse
        {
            UserId = _currentUser.UserId,
            OrganizationId = _currentUser.OrganizationId,
            OrganizationName = _currentUser.OrganizationName,
            Email = _currentUser.Email,
            DisplayName = _currentUser.DisplayName,
            Roles = _currentUser.Roles.ToArray(),
            Permissions = _currentUser.Permissions.ToArray()
        });
    }
}
