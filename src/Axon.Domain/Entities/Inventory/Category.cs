using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Inventory;

public class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private Category()
    {
    }

    public static Category Create(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre de la categoría es obligatorio.");
        }

        return new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            IsActive = true
        };
    }

    public void Update(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre de la categoría es obligatorio.");
        }

        Name = name;
        Description = description;
    }
}
