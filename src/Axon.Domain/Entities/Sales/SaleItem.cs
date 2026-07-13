using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Sales;

public class SaleItem
{
    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string ProductSku { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal Discount { get; private set; }
    public decimal Subtotal { get; private set; }

    private SaleItem()
    {
    }

    public static SaleItem Create(
        Guid saleId,
        Guid productId,
        string productName,
        string productSku,
        decimal unitPrice,
        int quantity,
        decimal discount = 0)
    {
        if (quantity <= 0)
        {
            throw new DomainException("La cantidad debe ser mayor a cero.");
        }

        if (unitPrice <= 0)
        {
            throw new DomainException("El precio unitario debe ser mayor a cero.");
        }

        if (discount < 0)
        {
            throw new DomainException("El descuento no puede ser negativo.");
        }

        var grossSubtotal = unitPrice * quantity;

        if (discount >= grossSubtotal)
        {
            throw new DomainException("El descuento no puede ser mayor al subtotal");
        }

        return new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = productId,
            ProductName = productName,
            ProductSku = productSku,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Discount = discount,
            Subtotal = grossSubtotal - discount
        };
    }
}
