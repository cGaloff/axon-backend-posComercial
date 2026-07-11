using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Axon.Application.Auth.Commands;
using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Axon.Infrastructure.Security;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public LoginResult GenerateToken(User user, ITenantContext tenantContext)
    {
        var jwtSettings = _configuration.GetSection("Jwt");

        var key = jwtSettings["Key"]
            ?? throw new InvalidOperationException("La clave 'Jwt:Key' no está configurada.");
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        var expiresInMinutes = jwtSettings.GetValue<int>("ExpiresInMinutes");

        var permissions = user.Role!.Permissions.Select(p => p.Key).ToArray();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.FullName),
            new("role", user.Role.Name),
            new("tenant_schema", tenantContext.SchemaName),
            new("tenant_slug", tenantContext.TenantSlug),
            new("permissions", string.Join(",", permissions))
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResult(
            accessToken,
            Guid.NewGuid().ToString(),
            expiresAt,
            user.FullName,
            user.Role.Name,
            permissions);
    }
}
