using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Taxes;

public class TaxType
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Code { get; private set; }
    public bool IsActive { get; private set; }

    private TaxType()
    {
    }

    public static TaxType Create(string name, string? code = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del impuesto es obligatorio.");
        }

        return new TaxType
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
            IsActive = true
        };
    }

    public void Update(string name, string? code)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del impuesto es obligatorio.");
        }

        Name = name;
        Code = code;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }
}
