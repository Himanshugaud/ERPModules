using System.IdentityModel.Tokens.Jwt;
using System.Text;
using ERP.Application.Identity;
using ERP.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ERP.Security.Tests;

public sealed class JwtValidationTests
{
    private static readonly string SigningKey = new('k', 64);
    private static JwtTokenService Service() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ERP_JwtSigningKey"] = SigningKey }).Build());

    [Fact]
    public void IssuedTokenPreservesVerifiedTenantAndPermissionClaims()
    {
        var organizationId = Guid.NewGuid();
        var service = Service();
        var version = UserSessionValidator.GetCredentialVersion("synthetic-password-hash");
        var issued = service.Generate(Guid.NewGuid(), organizationId, "Synthetic", "user@example.test", "Synthetic",
            new[] { "VIEWER" }, new[] { "project.read" }, version);

        var principal = service.Validate(issued.Token);

        Assert.NotNull(principal);
        Assert.Equal(organizationId.ToString(), principal.FindFirst("organizationId")?.Value);
        Assert.Equal("project.read", principal.FindFirst("permissions")?.Value);
        Assert.Equal(version, principal.FindFirst("credentialVersion")?.Value);
    }

    [Theory]
    [InlineData("wrong-issuer", "erp-clients", false, false)]
    [InlineData("erp-api", "wrong-audience", false, false)]
    [InlineData("erp-api", "erp-clients", true, false)]
    [InlineData("erp-api", "erp-clients", false, true)]
    public void InvalidIssuerAudienceExpiryOrSignatureIsRejected(string issuer, string audience, bool expired, bool wrongKey)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(wrongKey ? new string('x', 64) : SigningKey));
        var token = new JwtSecurityToken(issuer, audience, notBefore: DateTime.UtcNow.AddHours(-2),
            expires: expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        Assert.Null(Service().Validate(new JwtSecurityTokenHandler().WriteToken(token)));
    }
}