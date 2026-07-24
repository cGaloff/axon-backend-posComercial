using Axon.Domain.Interfaces;

namespace Axon.Application.Tests.TestSupport;

public class FakeCurrentUserContext : ICurrentUserContext
{
    public Guid UserId { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = "test@example.com";

    public string FullName { get; set; } = "Usuario de prueba";

    public string Role { get; set; } = "Propietario";

    public IEnumerable<string> Permissions { get; set; } = new List<string>();

    public bool HasPermission(string permission) => Permissions.Contains(permission);

    public bool IsInRole(string role) => string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
}
