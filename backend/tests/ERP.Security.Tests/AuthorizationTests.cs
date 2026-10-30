using System.Security.Claims;
using ERP.Api.Middleware;
using ERP.Api.Security;
using ERP.Application.Abstractions;
using ERP.Application.Identity;
using ERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ERP.Security.Tests;

public sealed class AuthorizationTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void DevelopmentHeadersRequireExplicitOptIn(string? enabled, bool authenticated)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString();
        http.Request.Headers["X-Organization-Id"] = Guid.NewGuid().ToString();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ERP_DevAuthEnabled"] = enabled, ["AZURE_FUNCTIONS_ENVIRONMENT"] = "Development"
        }).Build();

        Assert.Equal(authenticated, new CurrentUser(new HttpContextAccessor { HttpContext = http }, configuration).IsAuthenticated);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void DevelopmentHeadersCannotAuthenticateOutsideDevelopment(string? environment)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString();
        http.Request.Headers["X-Organization-Id"] = Guid.NewGuid().ToString();
        http.Request.Headers["X-Permissions"] = "stock.adjust";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ERP_DevAuthEnabled"] = "true", ["AZURE_FUNCTIONS_ENVIRONMENT"] = environment
        }).Build();

        var user = new CurrentUser(new HttpContextAccessor { HttpContext = http }, configuration);

        Assert.False(user.IsAuthenticated);
        Assert.Empty(user.Permissions);
    }

    [Fact]
    public void ValidatedIdentityWinsOverSpoofedTenantHeader()
    {
        var organizationId = Guid.NewGuid();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()), new Claim("organizationId", organizationId.ToString())
            }, "Bearer"))
        };
        http.Request.Headers["X-Organization-Id"] = Guid.NewGuid().ToString();
        var user = new CurrentUser(new HttpContextAccessor { HttpContext = http }, new ConfigurationBuilder().Build());

        Assert.True(user.IsAuthenticated);
        Assert.Equal(organizationId, user.OrganizationId);
    }

    [Fact]
    public void AnonymousCallerCannotPassPermissionGuard()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(identity => identity.HasPermission(It.IsAny<string>())).Returns(true);
        Assert.Throws<UnauthorizedException>(() => new AuthorizationGuard(user.Object).Require("stock.adjust"));
    }

    [Fact]
    public void AuthenticatedCallerWithoutPermissionIsForbidden()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(identity => identity.IsAuthenticated).Returns(true);
        Assert.Throws<ForbiddenException>(() => new AuthorizationGuard(user.Object).Require("stock.adjust"));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("revoked")]
    [InlineData("invalid-token")]
    [InlineData("store-unavailable")]
    [InlineData("anonymous")]
    public async Task MiddlewareRequiresVerifiedCurrentSessionBeforeExecutingEndpoint(string scenario)
    {
        var http = new DefaultHttpContext();
        if (scenario != "anonymous") http.Request.Headers.Authorization = "Bearer synthetic-token";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", Guid.NewGuid().ToString()) }, "Bearer"));
        var tokens = new Mock<IJwtTokenService>();
        tokens.Setup(service => service.Validate("synthetic-token"))
            .Returns(scenario == "invalid-token" ? null : principal);
        var sessions = new Mock<IUserSessionValidator>();
        sessions.Setup(service => service.IsValidAsync(principal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario == "valid");
        if (scenario == "store-unavailable")
            sessions.Setup(service => service.IsValidAsync(principal, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Synthetic store failure"));
        var services = new Mock<IServiceProvider>();
        services.Setup(provider => provider.GetService(typeof(IUserSessionValidator))).Returns(sessions.Object);
        var context = new Mock<FunctionContext>();
        context.SetupGet(function => function.InstanceServices).Returns(services.Object);
        context.SetupGet(function => function.Items).Returns(new Dictionary<object, object> { ["HttpRequestContext"] = http });
        Assert.Same(http, context.Object.GetHttpContext());
        var reachedEndpoint = false;
        FunctionExecutionDelegate next = _ => { reachedEndpoint = true; return Task.CompletedTask; };
        var middleware = new JwtAuthenticationMiddleware(tokens.Object);

        if (scenario is "revoked" or "invalid-token")
            await Assert.ThrowsAsync<UnauthorizedException>(() => middleware.Invoke(context.Object, next));
        else if (scenario == "store-unavailable")
            await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.Invoke(context.Object, next));
        else
            await middleware.Invoke(context.Object, next);

        Assert.Equal(scenario is "valid" or "anonymous", reachedEndpoint);
        if (scenario == "valid") Assert.Same(principal, http.User);
        if (scenario is "anonymous" or "invalid-token") sessions.VerifyNoOtherCalls();
    }
}