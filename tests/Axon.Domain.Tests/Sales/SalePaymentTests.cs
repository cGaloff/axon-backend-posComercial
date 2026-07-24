using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;

namespace Axon.Domain.Tests.Sales;

public class SalePaymentTests
{
    private static Sale CreateSaleWithTotal(decimal unitPrice, int quantity = 1)
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice, quantity);
        sale.AddItem(item);
        return sale;
    }

    // Caso simple: venta pagada 100% en efectivo. No debe romperse respecto al
    // comportamiento anterior (un solo pago cubre el total, con vuelto si aplica).
    [Fact]
    public void AddPayment_PaidFullyInCash_CoversTotalAndComputesChange()
    {
        var sale = CreateSaleWithTotal(90000m);

        var payment = SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total, amountTendered: 100000m);
        sale.AddPayment(payment);

        sale.EnsurePaymentsMatchTotal();

        var single = Assert.Single(sale.Payments);
        Assert.Equal(90000m, single.Amount);
        Assert.Equal(100000m, single.AmountTendered);
        Assert.Equal(10000m, single.Change);
        Assert.Equal(SaleStatus.Completed, sale.Status);
    }

    // Venta dividida entre 2 métodos que suman exacto el total.
    [Fact]
    public void AddPayment_SplitBetweenTwoMethodsSummingExactly_Succeeds()
    {
        var sale = CreateSaleWithTotal(100m);

        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, 60m));
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Card, 40m));

        sale.EnsurePaymentsMatchTotal();

        Assert.Equal(2, sale.Payments.Count);
        Assert.Equal(100m, sale.Payments.Sum(p => p.Amount));
        // Al incluir un pago con tarjeta, la venta queda pendiente de confirmación
        // (igual que en el modelo anterior de un solo método de pago).
        Assert.Equal(SaleStatus.PendingPayment, sale.Status);
    }

    [Fact]
    public void AddPayment_SplitBetweenCashAndCredit_KeepsStatusCompleted()
    {
        var sale = CreateSaleWithTotal(100m);

        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, 60m));
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Credit, 40m));

        Assert.Equal(SaleStatus.Completed, sale.Status);
    }

    // Venta con suma de pagos que no cuadra con el total: debe rechazarse.
    [Fact]
    public void EnsurePaymentsMatchTotal_WhenSumDoesNotMatchTotal_Throws()
    {
        var sale = CreateSaleWithTotal(100m);

        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, 90m));

        Assert.Throws<DomainException>(() => sale.EnsurePaymentsMatchTotal());
    }

    // Diferencia de redondeo dentro de la tolerancia definida (1 centavo): debe
    // aceptarse.
    [Fact]
    public void EnsurePaymentsMatchTotal_WithDifferenceWithinTolerance_Succeeds()
    {
        var sale = CreateSaleWithTotal(100m);

        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, 60.005m));
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Card, 39.995m));

        // Suma = 100.00 exacto en este caso; probamos también el límite explícito
        // de la tolerancia con un segundo escenario a continuación.
        sale.EnsurePaymentsMatchTotal();

        var slightlyOff = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(slightlyOff.Id, Guid.NewGuid(), "Producto", "SKU-002", 100m, 1);
        slightlyOff.AddItem(item);

        // 99.99 está exactamente dentro de la tolerancia de 0.01 respecto a 100.00.
        slightlyOff.AddPayment(SalePayment.Create(slightlyOff.Id, PaymentMethod.Cash, 99.99m));
        slightlyOff.EnsurePaymentsMatchTotal();
    }

    [Fact]
    public void EnsurePaymentsMatchTotal_WhenDifferenceExceedsTolerance_Throws()
    {
        var sale = CreateSaleWithTotal(100m);

        // 99.98 está a 0.02 del total: por encima de la tolerancia de 0.01.
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, 99.98m));

        Assert.Throws<DomainException>(() => sale.EnsurePaymentsMatchTotal());
    }

    // Revalidación del hallazgo del Prompt 2 (Sale.Total = suma de subtotales de
    // items): agregar pagos no debe alterar el total en absoluto, ni siquiera
    // cuando la venta tiene varias líneas para el mismo producto (mismo escenario
    // cubierto en SaleTests.AddItem_WithDuplicateProductAcrossTwoLines...).
    [Fact]
    public void AddPayment_DoesNotAffectTotal_WhichRemainsSumOfItemSubtotals()
    {
        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var productId = Guid.NewGuid();

        var item1 = SaleItem.Create(sale.Id, productId, "Producto A", "SKU-A", unitPrice: 1000m, quantity: 3);
        var item2 = SaleItem.Create(sale.Id, productId, "Producto A", "SKU-A", unitPrice: 1000m, quantity: 3);
        sale.AddItem(item1);
        sale.AddItem(item2);

        var totalBeforePayments = sale.Total;
        Assert.Equal(6000m, totalBeforePayments);

        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, 3000m));
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Card, 3000m));

        Assert.Equal(totalBeforePayments, sale.Total);
        sale.EnsurePaymentsMatchTotal();
    }
}
