using System.Security.Claims;
using ERP.Application.Abstractions;
using ERP.Application.Identity;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;
using Moq;
using Xunit;

namespace ERP.Security.Tests;

public sealed class AuthenticationTests
{
    [Theory]
    [InlineData("Synthetic-Test-Password1", true)]
    [InlineData("incorrect", false)]
    [InlineData(null, false)]
    public async Task ProvisionedAccountRequiresCorrectPassword(string? suppliedPassword, bool accepted)
    {
        var organization = new Organization { Id = Guid.NewGuid(), Code = "TEST", Name = "Synthetic" };
        var user = new User { Id = Guid.NewGuid(), OrganizationId = organization.Id, Email = "user@example.test" };
        var organizations = new Mock<IOrganizationRepository>();
        var users = new Mock<IUserRepository>();
        var tokens = new Mock<IJwtTokenService>();
        organizations.Setup(repository => repository.GetByCodeAsync("TEST", It.IsAny<CancellationToken>())).ReturnsAsync(organization);
        users.Setup(repository => repository.GetAsync(organization.Id, user.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        users.Setup(repository => repository.GetByEmailAsync(organization.Id, user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        users.Setup(repository => repository.GetRoleNamesAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<string>());
        users.Setup(repository => repository.GetPermissionCodesAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<string>());
        tokens.Setup(service => service.Generate(user.Id, organization.Id, organization.Name, user.Email, user.DisplayName,
            It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
            It.Is<string>(version => version == UserSessionValidator.GetCredentialVersion(user.PasswordHash!))))
            .Returns(("synthetic-token", DateTime.UtcNow.AddHours(1)));
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(context => context.OrganizationId).Returns(organization.Id);
        var employees = new UserService(users.Object, Mock.Of<IRoleRepository>(), tenant.Object, Mock.Of<IUnitOfWork>(),
            Mock.Of<IAuditWriter>(), Mock.Of<IClock>(), Mock.Of<IDepartmentRepository>());
        await employees.SetPasswordAsync(user.Id, new SetUserPasswordRequest { Password = "Synthetic-Test-Password1" });
        var authentication = new AuthService(organizations.Object, users.Object, tokens.Object);
        var request = new LoginRequest { OrganizationCode = "TEST", Email = user.Email, Password = suppliedPassword };

        if (accepted)
            Assert.Equal("synthetic-token", (await authentication.LoginAsync(request)).AccessToken);
        else
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => authentication.LoginAsync(request));
            tokens.VerifyNoOtherCalls();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("anything")]
    public async Task AccountWithoutPasswordCannotObtainToken(string? password)
    {
        var organization = new Organization { Id = Guid.NewGuid(), Code = "TEST", Name = "Synthetic" };
        var user = new User { Id = Guid.NewGuid(), OrganizationId = organization.Id, Email = "user@example.test" };
        var organizations = new Mock<IOrganizationRepository>();
        var users = new Mock<IUserRepository>();
        var tokens = new Mock<IJwtTokenService>();
        organizations.Setup(repository => repository.GetByCodeAsync("TEST", It.IsAny<CancellationToken>()))
            .ReturnsAsync(organization);
        users.Setup(repository => repository.GetByEmailAsync(organization.Id, user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        users.Setup(repository => repository.GetRoleNamesAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        users.Setup(repository => repository.GetPermissionCodesAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        var service = new AuthService(organizations.Object, users.Object, tokens.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(new LoginRequest
        {
            OrganizationCode = "TEST", Email = user.Email, Password = password
        }));

        tokens.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("disabled-user", false)]
    [InlineData("disabled-organization", false)]
    [InlineData("missing-user", false)]
    [InlineData("missing-organization", false)]
    [InlineData("password-reset", false)]
    [InlineData("cleared-password", false)]
    [InlineData("changed-role", false)]
    [InlineData("changed-permission", false)]
    [InlineData("legacy-token", false)]
    [InlineData("foreign-user", false)]
    [InlineData("invalid-user-id", false)]
    [InlineData("invalid-organization-id", false)]
    [InlineData("anonymous", false)]
    public async Task SessionRequiresCurrentActiveAccountAndAuthorization(string scenario, bool accepted)
    {
        var organization = new Organization { Id = Guid.NewGuid(), Code = "TEST", Name = "Synthetic" };
        var user = new User { Id = Guid.NewGuid(), OrganizationId = organization.Id, PasswordHash = "synthetic-password-hash" };
        var claims = new List<Claim>
        {
            new("sub", scenario == "invalid-user-id" ? "invalid" : user.Id.ToString()),
            new("organizationId", scenario == "invalid-organization-id" ? "invalid" : organization.Id.ToString()),
            new("roles", "EMPLOYEE"),
            new("permissions", "projects.read")
        };
        if (scenario != "legacy-token")
            claims.Add(new Claim("credentialVersion", UserSessionValidator.GetCredentialVersion(user.PasswordHash)));
        if (scenario == "disabled-user") user.Status = "INACTIVE";
        if (scenario == "disabled-organization") organization.Status = "INACTIVE";
        if (scenario == "password-reset") user.PasswordHash = "replacement-synthetic-hash";
        if (scenario == "cleared-password") user.PasswordHash = null;
        if (scenario == "foreign-user") user.OrganizationId = Guid.NewGuid();
        var organizations = new Mock<IOrganizationRepository>();
        var users = new Mock<IUserRepository>();
        organizations.Setup(repository => repository.GetAsync(organization.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario == "missing-organization" ? null : organization);
        users.Setup(repository => repository.GetAsync(organization.Id, user.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario == "missing-user" ? null : user);
        users.Setup(repository => repository.GetRoleNamesAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario == "changed-role" ? Array.Empty<string>() : new[] { "EMPLOYEE" });
        users.Setup(repository => repository.GetPermissionCodesAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario == "changed-permission" ? Array.Empty<string>() : new[] { "projects.read" });
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, scenario == "anonymous" ? null : "Bearer"));

        Assert.Equal(accepted, await new UserSessionValidator(organizations.Object, users.Object).IsValidAsync(principal));
    }
}