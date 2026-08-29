using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ERP.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Infrastructure.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _lifetimeHours;
    private readonly SymmetricSecurityKey _key;

    public JwtTokenService(IConfiguration config)
    {
        var signingKey = config["ERP_JwtSigningKey"]
            ?? throw new InvalidOperationException("ERP_JwtSigningKey is not configured.");
        _issuer = config["ERP_JwtIssuer"] ?? "erp-api";
        _audience = config["ERP_JwtAudience"] ?? "erp-clients";
        _lifetimeHours = int.TryParse(config["ERP_JwtLifetimeHours"], out var h) ? h : 8;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }

    public (string Token, DateTime ExpiresAt) Generate(
        Guid userId, Guid organizationId, string? organizationName, string? email, string? name,
        IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddHours(_lifetimeHours);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("organizationId", organizationId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (!string.IsNullOrEmpty(email)) claims.Add(new Claim("email", email));
        if (!string.IsNullOrEmpty(name)) claims.Add(new Claim("name", name));
        if (!string.IsNullOrEmpty(organizationName)) claims.Add(new Claim("orgName", organizationName));
        claims.AddRange(roles.Select(r => new Claim("roles", r)));
        claims.AddRange(permissions.Select(p => new Claim("permissions", p)));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public ClaimsPrincipal? Validate(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }
}
