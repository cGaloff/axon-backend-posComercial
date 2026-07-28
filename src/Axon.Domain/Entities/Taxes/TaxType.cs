using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Taxes;

public class TaxType
{
    // El IVA en Colombia solo admite estas tarifas (19%, 10%, 5%) o exento
    // (0%) — no un porcentaje libre como el resto de los impuestos.
    public static readonly IReadOnlySet<decimal> AllowedIvaPercentages = new HashSet<decimal> { 0m, 5m, 10m, 19m };

    public Guid Id { get; private set; }
    public TaxCode Code { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private TaxType()
    {
    }

    // El catálogo de impuestos es fijo (los 8 valores de TaxCode) y se
    // provisiona únicamente desde el seed/migraciones de cada tenant — ya no
    // existe un comando de aplicación para crear impuestos personalizados.
    public static TaxType Create(TaxCode code, string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del impuesto es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("La descripción del impuesto es obligatoria.");
        }

        return new TaxType
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = description,
            IsActive = true
        };
    }

    public static bool IsValidPercentageFor(TaxCode code, decimal percentage) =>
        code != TaxCode.Iva || AllowedIvaPercentages.Contains(percentage);

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }
}
