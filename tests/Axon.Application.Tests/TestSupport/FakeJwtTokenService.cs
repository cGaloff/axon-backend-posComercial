using Axon.Application.Auth.Commands;
using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Interfaces;

namespace Axon.Application.Tests.TestSupport;

public class FakeJwtTokenService : IJwtTokenService
{
    public LoginResult GenerateToken(User user, ITenantContext tenantContext) =>
        new("fake-access-token", "fake-refresh-token", DateTime.UtcNow.AddHours(1), user.FullName, user.Role?.Name ?? string.Empty, Array.Empty<string>());
}
