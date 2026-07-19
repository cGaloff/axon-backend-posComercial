using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Axon.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Axon.Infrastructure.Security;

public class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid UserId
    {
        get
        {
            var subClaim = User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(subClaim, out var userId) ? userId : Guid.Empty;
        }
    }

    public string Email => User?.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? string.Empty;

    public string FullName => User?.FindFirst("name")?.Value ?? string.Empty;

    public string Role => User?.FindFirst("role")?.Value ?? string.Empty;

    public IEnumerable<string> Permissions
    {
        get
        {
            var permissionsClaim = User?.FindFirst("permissions")?.Value;

            if (string.IsNullOrEmpty(permissionsClaim))
            {
                return Enumerable.Empty<string>();
            }

            return permissionsClaim.Split(',', StringSplitOptions.RemoveEmptyEntries);
        }
    }

    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsInRole(string role)
    {
        return string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
    }
}
