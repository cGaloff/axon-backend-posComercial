using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.CashRegister;

public class CashRegister
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    private CashRegister()
    {
    }

    public static CashRegister Create(string name, string description, bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre de la caja es obligatorio.");
        }

        return new CashRegister
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            IsDefault = isDefault,
            IsActive = true
        };
    }
}
