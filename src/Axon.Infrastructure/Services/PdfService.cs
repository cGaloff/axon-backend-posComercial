using Axon.Domain.Entities;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Interfaces;
using Net.Codecrete.QrCodeGenerator;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Axon.Infrastructure.Services;

public class PdfService : IPdfService
{
    private const float ReceiptWidthMm = 76;
    private const float MarginMm = 2;

    private static readonly TimeZoneInfo ColombiaTimeZone = ResolveColombiaTimeZone();

    private readonly IHttpClientFactory _httpClientFactory;

    public PdfService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public byte[] GenerateSaleReceipt(Sale sale, TenantConfig config)
    {
        var logoBytes = TryDownloadLogo(config.LogoUrl);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(ReceiptWidthMm, Unit.Millimetre);
                page.Margin(MarginMm, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Content().Column(column =>
                {
                    column.Item().Element(c => ComposeHeader(c, config, logoBytes));
                    column.Item().PaddingTop(1, Unit.Millimetre).Element(c => ComposeSaleData(c, sale));
                    column.Item().PaddingTop(1, Unit.Millimetre).Element(c => ComposeItemsTable(c, sale));
                    column.Item().PaddingTop(1, Unit.Millimetre).Element(c => ComposeTotals(c, sale, config));
                    column.Item().PaddingTop(2, Unit.Millimetre).Element(c => ComposeQrCode(c, sale, config));
                    column.Item().PaddingTop(2, Unit.Millimetre).Element(c => ComposeFooter(c, config));
                });
            });
        });

        return document.GeneratePdf();
    }

    private static TimeZoneInfo ResolveColombiaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback para Windows sin datos IANA (el ID de Windows es distinto al de Linux/Mac).
            return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
        }
    }

    private static DateTime ToColombiaTime(DateTime utcDateTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, ColombiaTimeZone);
    }

    private byte[]? TryDownloadLogo(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            // GenerateSaleReceipt es síncrono por diseño (así lo consumen ProcessSaleCommandHandler
            // y GetSaleReceiptQueryHandler), así que se bloquea aquí en vez de propagar async
            // a toda la cadena de llamadas.
            return client.GetByteArrayAsync(logoUrl).GetAwaiter().GetResult();
        }
        catch
        {
            // Logo inválido, inaccesible o URL rota: se omite silenciosamente, nunca rompe el recibo.
            return null;
        }
    }

    private static void ComposeHeader(IContainer container, TenantConfig config, byte[]? logoBytes)
    {
        container.Column(column =>
        {
            if (logoBytes is not null)
            {
                column.Item().AlignCenter().Height(30, Unit.Millimetre).Image(logoBytes);
            }

            column.Item().AlignCenter().Text(config.BusinessName).FontSize(11).Bold();

            if (!string.IsNullOrWhiteSpace(config.Nit))
            {
                column.Item().AlignCenter().Text($"NIT: {config.Nit}").FontSize(8);
            }

            if (!string.IsNullOrWhiteSpace(config.Address))
            {
                column.Item().AlignCenter().Text(config.Address).FontSize(7);
            }

            if (!string.IsNullOrWhiteSpace(config.Phone))
            {
                column.Item().AlignCenter().Text($"Tel: {config.Phone}").FontSize(7);
            }

            if (!string.IsNullOrWhiteSpace(config.Website))
            {
                column.Item().AlignCenter().Text(config.Website).FontSize(7);
            }

            column.Item().PaddingTop(1, Unit.Millimetre).LineHorizontal(1);
        });
    }

    private static void ComposeSaleData(IContainer container, Sale sale)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Text("TIQUETE DE VENTA").FontSize(9).Bold();
            column.Item().AlignCenter().Text($"No. {sale.SaleNumber}").FontSize(9);

            column.Item().PaddingVertical(1, Unit.Millimetre).LineHorizontal(0.5f);

            var localCreatedAt = ToColombiaTime(sale.CreatedAt);
            column.Item().Text($"Fecha: {localCreatedAt:dd/MM/yyyy}  Hora: {localCreatedAt:HH:mm}");

            // El cajero se muestra como Guid hasta que exista un servicio que resuelva
            // el UserId a un nombre legible (p. ej. GetUserName en ICurrentUserContext).
            column.Item().Text($"Cajero: {sale.CreatedBy}");

            if (!string.IsNullOrWhiteSpace(sale.CustomerName))
            {
                column.Item().Text($"Cliente: {sale.CustomerName}");
            }

            column.Item().PaddingTop(1, Unit.Millimetre).LineHorizontal(1);
        });
    }

    private static void ComposeItemsTable(IContainer container, Sale sale)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(5);
                    columns.RelativeColumn(3);
                });

                table.Header(header =>
                {
                    header.Cell().Text("CANT").Bold();
                    header.Cell().Text("DESCRIPCIÓN").Bold();
                    header.Cell().AlignRight().Text("TOTAL").Bold();
                });

                foreach (var item in sale.Items)
                {
                    table.Cell().Text(item.Quantity.ToString());
                    table.Cell().Text(item.ProductName);
                    table.Cell().AlignRight().Text(item.Subtotal.ToString("N0"));

                    if (item.TaxPercentage > 0)
                    {
                        table.Cell();
                        table.Cell().Text($"  IVA {item.TaxPercentage:0.##}%: {item.TaxAmount:N0}").FontSize(7);
                        table.Cell();
                    }

                    if (item.Discount > 0)
                    {
                        table.Cell();
                        table.Cell().Text($"  Desc: -{item.Discount:N0}").FontSize(7);
                        table.Cell();
                    }
                }
            });

            column.Item().PaddingTop(1, Unit.Millimetre).LineHorizontal(1);
        });
    }

    private static void ComposeTotals(IContainer container, Sale sale, TenantConfig config)
    {
        container.Column(column =>
        {
            if (config.IsResponsableIva)
            {
                var subtotalBase = sale.Items.Sum(i => i.SubtotalBase);

                column.Item().Row(row =>
                {
                    row.RelativeItem(1).Text("Subtotal (sin IVA):");
                    row.RelativeItem(1).AlignRight().Text(subtotalBase.ToString("N0"));
                });

                var taxGroups = sale.Items
                    .Where(i => i.TaxPercentage > 0)
                    .GroupBy(i => i.TaxPercentage)
                    .OrderBy(g => g.Key);

                foreach (var group in taxGroups)
                {
                    var amount = group.Sum(i => i.TaxAmount);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem(1).Text($"IVA {group.Key:0.##}%:");
                        row.RelativeItem(1).AlignRight().Text(amount.ToString("N0"));
                    });
                }
            }

            column.Item().PaddingTop(1, Unit.Millimetre).LineHorizontal(1);

            column.Item().PaddingTop(1, Unit.Millimetre).Row(row =>
            {
                row.RelativeItem(1).Text("TOTAL:").FontSize(10).Bold();
                row.RelativeItem(1).AlignRight().Text(sale.Total.ToString("N0")).FontSize(10).Bold();
            });

            switch (sale.PaymentMethod)
            {
                case PaymentMethod.Cash:
                    column.Item().Row(row =>
                    {
                        row.RelativeItem(1).Text("Efectivo:");
                        row.RelativeItem(1).AlignRight().Text(sale.AmountPaid.ToString("N0"));
                    });
                    column.Item().Row(row =>
                    {
                        row.RelativeItem(1).Text("Cambio:");
                        row.RelativeItem(1).AlignRight().Text(sale.Change.ToString("N0"));
                    });
                    break;

                case PaymentMethod.Card:
                    column.Item().AlignCenter().Text("Pago con tarjeta");
                    break;

                case PaymentMethod.Transfer:
                    column.Item().AlignCenter().Text("Pago por transferencia");
                    break;

                case PaymentMethod.Credit:
                    column.Item().AlignCenter().Text("Crédito / Fiado");
                    break;
            }
        });
    }

    private static void ComposeQrCode(IContainer container, Sale sale, TenantConfig config)
    {
        var localCreatedAt = ToColombiaTime(sale.CreatedAt);
        var qrContent = $"Venta: {sale.SaleNumber} | Fecha: {localCreatedAt:dd/MM/yyyy} | Total: {sale.Total:N0} | Tienda: {config.BusinessName}";
        var qrCode = QrCode.EncodeText(qrContent, QrCode.Ecc.Medium);
        var qrBytes = qrCode.ToBmpBitmap(border: 2, scale: 6);

        container.AlignCenter().Width(30, Unit.Millimetre).Image(qrBytes);
    }

    private static void ComposeFooter(IContainer container, TenantConfig config)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1);
            column.Item().PaddingTop(1, Unit.Millimetre).AlignCenter().Text("Gracias por su compra").Italic();
            column.Item().AlignCenter().Text("Conserve este tiquete").FontSize(7);

            if (!string.IsNullOrWhiteSpace(config.Website))
            {
                column.Item().AlignCenter().Text(config.Website).FontSize(7);
            }

            column.Item().PaddingTop(1, Unit.Millimetre).AlignCenter().Text("Software: www.axoncompany.com.co").FontSize(6);
        });
    }
}
