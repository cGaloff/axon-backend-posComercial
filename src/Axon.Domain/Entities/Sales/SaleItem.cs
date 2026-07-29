using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities.Sales;

public class SaleItem
{
    private readonly List<SaleItemTax> _taxes = new();

    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string ProductSku { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal Discount { get; private set; }

    // Null si Discount se dio como monto fijo — en ese caso la factura solo
    // muestra el monto. Si se dio como % (ej. "5% de descuento a este
    // producto"), guarda ESE porcentaje tal cual, solo para mostrarlo junto al
    // monto en la factura ("Desc: (5%)") — no participa en ningún cálculo, el
    // monto ya convertido en Discount es el que se usa siempre.
    public decimal? DiscountPercentage { get; private set; }

    // Costo unitario del producto AL MOMENTO DE LA VENTA (snapshot de
    // Product.Cost, igual que UnitPrice y los impuestos) — no el costo actual.
    // Product.Cost es un costo promedio ponderado que cambia con cada compra
    // nueva; sin este snapshot, el reporte de ganancia de una venta pasada
    // cambiaría retroactivamente cada vez que se actualice el costo del
    // producto. 0 = sin dato de costo (ventas anteriores a este campo, o
    // productos sin costo cargado).
    public decimal UnitCost { get; private set; }

    // Porción del descuento general de la venta (Sale.GeneralDiscountAmount)
    // que le corresponde a esta línea, repartida proporcionalmente al momento
    // de crear la venta (ver ProcessSaleCommandHandler). Separado de Discount
    // a propósito: Discount es SOLO lo que el cajero puso manualmente en este
    // producto puntual, para que la factura pueda mostrar cada concepto por
    // separado (línea "Desc:" por producto vs. una sola línea "Descuento
    // general" a nivel de venta) sin mezclarlos.
    public decimal GeneralDiscountShare { get; private set; }

    public decimal Subtotal { get; private set; }
    public decimal SubtotalBase { get; private set; }

    public IReadOnlyList<SaleItemTax> Taxes => _taxes;

    public decimal TotalTaxAmount => _taxes.Sum(t => t.Amount);

    private SaleItem()
    {
    }

    // appliedTaxes es el snapshot de impuestos vigentes al momento de la venta
    // (TaxTypeId + nombre + porcentaje, tomados de ProductTax/TaxType en ese
    // instante). Todos los impuestos se calculan sobre la MISMA base gravable
    // (no son compuestos entre sí) — decisión de diseño documentada en el
    // resumen del prompt 3, consistente con cómo IVA e ICA conviven hoy en
    // Colombia (ambos aplican sobre el valor de la venta, no uno sobre el otro).
    public static SaleItem Create(
        Guid saleId,
        Guid productId,
        string productName,
        string productSku,
        decimal unitPrice,
        int quantity,
        decimal discount = 0,
        decimal? discountPercentage = null,
        decimal generalDiscountShare = 0,
        decimal unitCost = 0,
        IEnumerable<(Guid TaxTypeId, string TaxTypeName, decimal Percentage)>? appliedTaxes = null)
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

        if (discountPercentage is < 0 or > 100)
        {
            throw new DomainException("El porcentaje de descuento debe estar entre 0 y 100.");
        }

        if (generalDiscountShare < 0)
        {
            throw new DomainException("El descuento general no puede ser negativo.");
        }

        if (unitCost < 0)
        {
            throw new DomainException("El costo unitario no puede ser negativo.");
        }

        var grossSubtotal = unitPrice * quantity;

        if (discount + generalDiscountShare >= grossSubtotal)
        {
            throw new DomainException("El descuento no puede ser mayor al subtotal");
        }

        var taxes = (appliedTaxes ?? Enumerable.Empty<(Guid, string, decimal)>()).ToList();
        var totalTaxRate = taxes.Sum(t => t.Percentage);

        // Subtotal incluye todos los impuestos aplicados (precio final que paga el
        // cliente); SubtotalBase es la base gravable obtenida al "desquitar" la suma
        // de tasas del subtotal con los dos descuentos ya aplicados (manual por
        // producto + la porción del descuento general que le tocó a esta línea).
        var subtotal = grossSubtotal - discount - generalDiscountShare;
        var subtotalBase = subtotal / (1 + totalTaxRate / 100);

        var id = Guid.NewGuid();

        var taxSnapshots = taxes
            .Select(t => SaleItemTax.Create(id, t.TaxTypeId, t.TaxTypeName, t.Percentage, subtotalBase * t.Percentage / 100))
            .ToList();

        var item = new SaleItem
        {
            Id = id,
            SaleId = saleId,
            ProductId = productId,
            ProductName = productName,
            ProductSku = productSku,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Discount = discount,
            DiscountPercentage = discountPercentage,
            GeneralDiscountShare = generalDiscountShare,
            UnitCost = unitCost,
            Subtotal = subtotal,
            SubtotalBase = subtotalBase
        };

        item._taxes.AddRange(taxSnapshots);

        return item;
    }
}
