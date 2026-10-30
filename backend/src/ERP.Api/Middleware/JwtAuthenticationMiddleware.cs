using ERP.Application.Abstractions;
using ERP.Application.Identity;
using ERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Api.Middleware;

/// Validates a Bearer JWT (issued by the login endpoint) and sets HttpContext.User.
public sealed class JwtAuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IJwtTokenService _jwt;
    public JwtAuthenticationMiddleware(IJwtTokenService jwt) => _jwt = jwt;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var http = context.GetHttpContext();
        if (http is not null)
        {
            var header = http.Request.Headers.Authorization.ToString();
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = header["Bearer ".Length..].Trim();
                var principal = _jwt.Validate(token);
                if (principal is null || !await context.InstanceServices.GetRequiredService<IUserSessionValidator>()
                    .IsValidAsync(principal, context.CancellationToken))
                    throw new UnauthorizedException("Session expired or invalid. Please sign in again.");
                http.User = principal;
            }
        }
        await next(context);
    }
}
