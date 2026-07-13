using Axon.Domain.Entities.Sales;
using Axon.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Axon.Infrastructure.Services;

public class PdfService : IPdfService
{
    public byte[] GenerateSaleReceipt(Sale sale, string businessName)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text(businessName).FontSize(16).Bold();
                    column.Item().Text($"Venta: {sale.SaleNumber}");
                    column.Item().Text($"Fecha: {sale.CreatedAt:dd/MM/yyyy HH:mm}");
                    column.Item().Text($"Método de pago: {sale.PaymentMethod}");
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Producto").Bold();
                        header.Cell().Text("Cant.").Bold();
                        header.Cell().Text("P. Unit.").Bold();
                        header.Cell().Text("Desc.").Bold();
                        header.Cell().Text("Subtotal").Bold();
                    });

                    foreach (var item in sale.Items)
                    {
                        table.Cell().Text(item.ProductName);
                        table.Cell().Text(item.Quantity.ToString());
                        table.Cell().Text(item.UnitPrice.ToString("N0"));
                        table.Cell().Text(item.Discount.ToString("N0"));
                        table.Cell().Text(item.Subtotal.ToString("N0"));
                    }
                });

                page.Footer().Column(column =>
                {
                    column.Item().AlignRight().Text($"Total: {sale.Total:N0}").Bold();

                    if (sale.PaymentMethod == PaymentMethod.Cash)
                    {
                        column.Item().AlignRight().Text($"Monto pagado: {sale.AmountPaid:N0}");
                        column.Item().AlignRight().Text($"Cambio: {sale.Change:N0}");
                    }

                    column.Item().PaddingTop(10).AlignCenter().Text("Gracias por su compra");
                });
            });
        });

        return document.GeneratePdf();
    }
}
