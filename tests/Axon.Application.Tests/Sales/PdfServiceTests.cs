using Axon.Domain.Entities.Sales;
using Axon.Domain.Entities.Taxes;
using Axon.Infrastructure.Services;
using QuestPDF.Infrastructure;
using TenantConfigEntity = Axon.Domain.Entities.TenantConfig;

namespace Axon.Application.Tests.Sales;

public class PdfServiceTests
{
    // Mismo self-declaration de licencia que Program.cs (QuestPDF exige esto
    // antes de generar cualquier documento, en producción y en pruebas).
    static PdfServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string CashierName = "Ana Martínez";

    private static void AssertValidPdf(byte[] pdfBytes)
    {
        Assert.NotEmpty(pdfBytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }

    [Fact]
    public void GenerateSaleReceipt_WithSinglePaymentMethod_ProducesValidPdf()
    {
        var service = new PdfService();

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid(), customerName: "Juan Pablo Gómez");
        var iva = TaxType.Create("IVA", "IVA");
        var item = SaleItem.Create(
            sale.Id, Guid.NewGuid(), "Camiseta Básica Hombre", "CAM-001",
            unitPrice: 39900m, quantity: 1, discount: 0,
            appliedTaxes: new[] { (iva.Id, "IVA", 19m) });
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total, amountTendered: sale.Total));

        var config = TenantConfigEntity.Create("Moda Urbana");

        var pdfBytes = service.GenerateSaleReceipt(sale, config, CashierName);

        AssertValidPdf(pdfBytes);
    }

    [Fact]
    public void GenerateSaleReceipt_WithoutCustomerName_ShowsConsumidorFinalInsteadOfBlank()
    {
        var service = new PdfService();

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 1000m, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total));

        var config = TenantConfigEntity.Create("Tienda de prueba");

        var pdfBytes = service.GenerateSaleReceipt(sale, config, CashierName);

        AssertValidPdf(pdfBytes);
    }

    [Fact]
    public void GenerateSaleReceipt_WithSplitPaymentAcrossThreeMethods_ProducesValidPdf()
    {
        var service = new PdfService();

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 90000m, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, 30000m, amountTendered: 30000m));
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Card, 30000m));
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Transfer, 30000m));

        var config = TenantConfigEntity.Create("Tienda de prueba");

        var pdfBytes = service.GenerateSaleReceipt(sale, config, CashierName);

        AssertValidPdf(pdfBytes);
    }

    [Fact]
    public void GenerateSaleReceipt_WithMultipleTaxTypesIncludingOneAtZeroPercent_ProducesValidPdf()
    {
        var service = new PdfService();

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var iva = TaxType.Create("IVA", "IVA19");
        var exento = TaxType.Create("Exento", "EXE");

        var taxedItem = SaleItem.Create(
            sale.Id, Guid.NewGuid(), "Producto Gravado", "SKU-001",
            unitPrice: 100000m, quantity: 1, discount: 0,
            appliedTaxes: new[] { (iva.Id, "IVA", 19m) });
        var exemptItem = SaleItem.Create(
            sale.Id, Guid.NewGuid(), "Producto Exento", "SKU-002",
            unitPrice: 50000m, quantity: 1, discount: 0,
            appliedTaxes: new[] { (exento.Id, "Exento", 0m) });

        sale.AddItem(taxedItem);
        sale.AddItem(exemptItem);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total, amountTendered: sale.Total));

        var config = TenantConfigEntity.Create("Tienda de prueba");

        var pdfBytes = service.GenerateSaleReceipt(sale, config, CashierName);

        AssertValidPdf(pdfBytes);
    }

    [Fact]
    public void GenerateSaleReceipt_WithZeroDiscount_ProducesValidPdf()
    {
        var service = new PdfService();

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 20000m, quantity: 1, discount: 0);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total));

        var config = TenantConfigEntity.Create("Tienda de prueba");

        var pdfBytes = service.GenerateSaleReceipt(sale, config, CashierName);

        AssertValidPdf(pdfBytes);
    }

    // El código que generaba el QR (ComposeQrCode) se eliminó por completo, así
    // que no puede aparecer ningún XObject de imagen rasterizada en el PDF
    // resultante — el único elemento gráfico ahora es el ícono genérico del
    // encabezado, que se renderiza como SVG vectorial, no como imagen embebida.
    [Fact]
    public void GenerateSaleReceipt_NeverEmbedsARasterImageObject()
    {
        var service = new PdfService();

        var sale = Sale.Create(Guid.NewGuid(), Guid.NewGuid());
        var item = SaleItem.Create(sale.Id, Guid.NewGuid(), "Producto", "SKU-001", unitPrice: 20000m, quantity: 1);
        sale.AddItem(item);
        sale.AddPayment(SalePayment.Create(sale.Id, PaymentMethod.Cash, sale.Total));

        var config = TenantConfigEntity.Create("Tienda de prueba");

        var pdfBytes = service.GenerateSaleReceipt(sale, config, CashierName);

        var pdfText = System.Text.Encoding.Latin1.GetString(pdfBytes);
        Assert.DoesNotContain("/Subtype/Image", pdfText.Replace(" ", ""));
    }
}
