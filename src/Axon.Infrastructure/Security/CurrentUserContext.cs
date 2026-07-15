using System.IdentityModel.Tokens.Jwt;
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

    public Guid UserId
    {
        get
        {
            var subClaim = _httpContextAccessor.HttpContext?.User
                .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return Guid.TryParse(subClaim, out var userId) ? userId : Guid.Empty;
        }
    }
}
