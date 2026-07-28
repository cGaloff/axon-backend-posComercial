namespace Axon.Domain.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    string Role { get; }
    IEnumerable<string> Permissions { get; }

    // Null = sin tope de descuento para el rol actual (ver Role.MaxDiscountPercentage).
    decimal? MaxDiscountPercentage { get; }

    bool HasPermission(string permission);
    bool IsInRole(string role);
}
