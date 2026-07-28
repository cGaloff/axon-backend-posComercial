using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities;

public class Role
{
    private readonly List<Permission> _permissions = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public string Description { get; private set; } = string.Empty;

    // Null = sin tope (Propietario/Administrador). Con valor = porcentaje máximo
    // de descuento que un usuario con este rol puede aplicar en una venta sin
    // autorización de un rol con tope superior (Matriz de Roles y Permisos v2,
    // regla transversal C).
    public decimal? MaxDiscountPercentage { get; private set; }

    public IReadOnlyList<Permission> Permissions => _permissions;

    private Role()
    {
    }

    public static Role Create(string name, string description, bool isSystem, decimal? maxDiscountPercentage = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Name is required.");
        }

        if (maxDiscountPercentage is < 0 or > 100)
        {
            throw new DomainException("El tope de descuento debe estar entre 0 y 100.");
        }

        return new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            IsSystem = isSystem,
            MaxDiscountPercentage = maxDiscountPercentage
        };
    }

    public void SetMaxDiscountPercentage(decimal? maxDiscountPercentage)
    {
        if (maxDiscountPercentage is < 0 or > 100)
        {
            throw new DomainException("El tope de descuento debe estar entre 0 y 100.");
        }

        MaxDiscountPercentage = maxDiscountPercentage;
    }

    public void AddPermission(Permission permission)
    {
        _permissions.Add(permission);
    }
}
