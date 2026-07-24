using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Inventory;

// Impuesto vigente configurado sobre un producto (catálogo TaxType del tenant).
// A diferencia de Sales.SaleItemTax, esto NO es un snapshot: refleja la
// configuración actual del producto y puede cambiar libremente en el tiempo.
public class ProductTax
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid TaxTypeId { get; private set; }
    public decimal Percentage { get; private set; }

    private ProductTax()
    {
    }

    public static ProductTax Create(Guid productId, Guid taxTypeId, decimal percentage)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("El producto es obligatorio.");
        }

        if (taxTypeId == Guid.Empty)
        {
            throw new DomainException("El tipo de impuesto es obligatorio.");
        }

        if (percentage < 0)
        {
            throw new DomainException("El porcentaje de impuesto no puede ser negativo.");
        }

        return new ProductTax
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            TaxTypeId = taxTypeId,
            Percentage = percentage
        };
    }
}
