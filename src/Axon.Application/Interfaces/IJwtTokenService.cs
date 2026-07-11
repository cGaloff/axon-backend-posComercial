using Axon.Application.Auth.Commands;
using Axon.Domain.Entities;
using Axon.Domain.Interfaces;

namespace Axon.Application.Interfaces;

public interface IJwtTokenService
{
    LoginResult GenerateToken(User user, ITenantContext tenantContext);
}
