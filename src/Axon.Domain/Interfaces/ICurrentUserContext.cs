namespace Axon.Domain.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    string Role { get; }
    IEnumerable<string> Permissions { get; }

    bool HasPermission(string permission);
    bool IsInRole(string role);
}
