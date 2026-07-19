using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Inventory;

public class Warehouse
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    private Warehouse()
    {
    }

    public static Warehouse Create(string name, string description, bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre de la bodega es obligatorio.");
        }

        return new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            IsDefault = isDefault,
            IsActive = true
        };
    }
}
