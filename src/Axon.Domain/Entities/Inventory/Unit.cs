using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Inventory;

public class Unit
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Abbreviation { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private Unit()
    {
    }

    public static Unit Create(string name, string abbreviation)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre de la unidad es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(abbreviation))
        {
            throw new DomainException("La abreviatura de la unidad es obligatoria.");
        }

        return new Unit
        {
            Id = Guid.NewGuid(),
            Name = name,
            Abbreviation = abbreviation,
            IsActive = true
        };
    }
}
