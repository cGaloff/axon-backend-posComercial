using Axon.Domain.Entities;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Axon.Infrastructure.Services;

public class PdfService : IPdfService
{
    private const float ReceiptWidthMm = 76;
    private const float MarginMm = 3;

    private static readonly TimeZoneInfo ColombiaTimeZone = ResolveColombiaTimeZone();

    // Ícono genérico (bolsa + percha de tienda), no ligado a la marca de ningún
    // tenant — se usa mientras no exista un mecanismo real de logo por tenant.
    // Se dibuja como SVG embebido (QuestPDF ya trae su propio renderizador SVG,
    // así que esto no agrega ninguna dependencia nueva) en vez de un archivo de
    // imagen externo, para no tener que empaquetar/versionar un asset binario.
    private const string GenericStoreIconSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
            <path d="M28 30 Q50 10 72 30" stroke="#222222" stroke-width="5" fill="none" stroke-linecap="round"/>
            <circle cx="50" cy="10" r="4" fill="#222222"/>
            <polygon points="24,30 76,30 84,92 16,92" fill="#222222"/>
            <path d="M38,30 Q38,14 50,14 Q62,14 62,30" stroke="#ffffff" stroke-width="5" fill="none" stroke-linecap="round"/>
        </svg>
        """;

    public byte[] GenerateSaleReceipt(Sale sale, TenantConfig config, string cashierName, string cashRegisterName)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(ReceiptWidthMm, Unit.Millimetre);
                page.Margin(MarginMm, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor("#222222"));

                page.Content().Column(column =>
                {
                    column.Item().Element(c => ComposeHeader(c, config));
                    column.Item().PaddingTop(2, Unit.Millimetre).Element(c => ComposeSaleData(c, sale, cashierName, cashRegisterName));
                    column.Item().PaddingTop(2, Unit.Millimetre).Element(c => ComposeItemsTable(c, sale));
                    column.Item().PaddingTop(2, Unit.Millimetre).Element(c => ComposeTotals(c, sale));
                    column.Item().PaddingTop(3, Unit.Millimetre).Element(c => ComposeTaxSummaryTable(c, sale));
                    column.Item().PaddingTop(3, Unit.Millimetre).Element(c => ComposeFooter(c, config));
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
            try
            {
                // Fallback para Windows sin datos IANA (el ID de Windows es distinto al de Linux/Mac).
                return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                // Ultimo recurso: no depende de NINGUN dato de zona horaria del sistema
                // operativo. Necesario porque contenedores Linux sin tzdata/ICU completos
                // (comun en imagenes minimalistas) hacen fallar los dos intentos de arriba
                // a la vez, tirando abajo la inicializacion estatica de esta clase entera.
                // Colombia no tiene horario de verano, asi que UTC-5 fijo es exacto siempre.
                return TimeZoneInfo.CreateCustomTimeZone("Colombia-Fixed-UTC-5", TimeSpan.FromHours(-5), "Colombia (fijo)", "Colombia (fijo)");
            }
        }
    }

    private static DateTime ToColombiaTime(DateTime utcDateTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, ColombiaTimeZone);
    }

    private static void ComposeHeader(IContainer container, TenantConfig config)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Width(18, Unit.Millimetre).Svg(GenericStoreIconSvg);

            column.Item().PaddingTop(1, Unit.Millimetre).AlignCenter().Text(config.BusinessName).FontSize(13).Bold();

            // Régimen tributario (IsResponsableIva) y correo SÍ existen en TenantConfig
            // y no se mostraban antes; horario de atención y redes sociales NO existen
            // en el modelo hoy, así que no se inventan — quedan como punto abierto.
            column.Item().AlignCenter().Text(config.IsResponsableIva ? "Responsable de IVA" : "No responsable de IVA")
                .FontSize(7).FontColor(Colors.Grey.Darken2);

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

            if (!string.IsNullOrWhiteSpace(config.Email))
            {
                column.Item().AlignCenter().Text(config.Email).FontSize(7);
            }

            if (!string.IsNullOrWhiteSpace(config.Website))
            {
                column.Item().AlignCenter().Text(config.Website).FontSize(7);
            }

            column.Item().PaddingTop(2, Unit.Millimetre).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
        });
    }

    private static void ComposeSaleData(IContainer container, Sale sale, string cashierName, string cashRegisterName)
    {
        container.Column(column =>
        {
            var localCreatedAt = ToColombiaTime(sale.CreatedAt);

            // Formato de número de factura (SaleNumber, ej. "VTA-20260726-4CD6D0"):
            // se deja tal cual — es una decisión de negocio pendiente de confirmar
            // (consecutivo simple vs. el formato actual), no un bug de este cambio.
            column.Item().AlignCenter().Text($"FACTURA POS No. {sale.SaleNumber}").FontSize(11).Bold();

            column.Item().PaddingVertical(1, Unit.Millimetre).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

            column.Item().Text($"Fecha: {localCreatedAt:dd/MM/yyyy}   Hora: {localCreatedAt:HH:mm}");
            column.Item().Text($"Cajero: {cashierName}");
            column.Item().Text($"Caja: {cashRegisterName}");
            column.Item().Text(!string.IsNullOrWhiteSpace(sale.CustomerName) ? $"Cliente: {sale.CustomerName}" : "Cliente: Consumidor Final");

            column.Item().PaddingTop(2, Unit.Millimetre).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
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
                    columns.RelativeColumn(3);
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).Text("CANT").Bold();
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).Text("DESCRIPCIÓN").Bold();
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).AlignRight().Text("PRECIO").Bold();
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).AlignRight().Text("TOTAL").Bold();
                });

                foreach (var item in sale.Items)
                {
                    table.Cell().PaddingTop(1, Unit.Millimetre).Text(item.Quantity.ToString());
                    table.Cell().PaddingTop(1, Unit.Millimetre).Text(item.ProductName);
                    table.Cell().PaddingTop(1, Unit.Millimetre).AlignRight().Text(item.UnitPrice.ToString("N0"));
                    table.Cell().PaddingTop(1, Unit.Millimetre).AlignRight().Text(item.Subtotal.ToString("N0"));

                    foreach (var tax in item.Taxes)
                    {
                        table.Cell().ColumnSpan(4).PaddingLeft(3, Unit.Millimetre)
                            .Text($"{tax.TaxTypeName} {tax.Percentage:0.####}%: {tax.Amount:N0}").FontSize(7).FontColor(Colors.Grey.Darken2);
                    }

                    if (item.Discount > 0)
                    {
                        table.Cell().ColumnSpan(4).PaddingLeft(3, Unit.Millimetre)
                            .Text($"Desc: -{item.Discount:N0}").FontSize(7).FontColor(Colors.Grey.Darken2);
                    }
                }
            });

            column.Item().PaddingTop(1, Unit.Millimetre).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
        });
    }

    // Estructura fija (Subtotal -> Descuento -> Base gravable -> Impuestos ->
    // TOTAL A PAGAR): son solo etiquetas de presentación, los valores subyacentes
    // son los mismos que ya calcula el dominio (SaleItem.Subtotal ya incluye
    // impuestos; SubtotalBase es la base "desquitada" de esos mismos impuestos).
    // El desglose por cada tipo de impuesto vive aparte, en ComposeTaxSummaryTable
    // (el catálogo de impuestos es libre por tenant, no una lista fija tipo
    // "IVA/Otros impuestos").
    private static void ComposeTotals(IContainer container, Sale sale)
    {
        container.Column(column =>
        {
            var grossSubtotal = sale.Items.Sum(i => i.UnitPrice * i.Quantity);
            var totalDiscount = sale.Items.Sum(i => i.Discount);
            var subtotalBase = sale.Items.Sum(i => i.SubtotalBase);
            var totalTax = sale.Items.Sum(i => i.TotalTaxAmount);

            void TotalLine(string label, decimal amount, bool bold = false)
            {
                column.Item().Row(row =>
                {
                    var labelText = row.RelativeItem(1).Text(label).FontSize(bold ? 10 : 8);
                    var amountText = row.RelativeItem(1).AlignRight().Text(amount.ToString("N0")).FontSize(bold ? 10 : 8);

                    if (bold)
                    {
                        labelText.Bold();
                        amountText.Bold();
                    }
                });
            }

            TotalLine("Subtotal", grossSubtotal);
            TotalLine("Descuento", -totalDiscount);
            TotalLine("Base Gravable", subtotalBase);
            TotalLine("Impuestos", totalTax);

            column.Item().PaddingTop(1, Unit.Millimetre).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(1, Unit.Millimetre).Element(c => { });

            TotalLine("TOTAL A PAGAR", sale.Total, bold: true);

            column.Item().PaddingTop(1, Unit.Millimetre).LineHorizontal(1).LineColor(Colors.Grey.Darken1);

            // Pagos divididos: una línea por método (método + monto en la misma
            // fila, sin desglosar cálculos internos); el efectivo además muestra lo
            // entregado y el vuelto de esa línea puntual (no aplica a tarjeta/
            // transferencia, que se cobran por el monto exacto).
            foreach (var payment in sale.Payments)
            {
                column.Item().PaddingTop(1, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem(1).Text($"Forma de Pago: {DescribePaymentMethod(payment.Method)}");
                    row.RelativeItem(1).AlignRight().Text(payment.Amount.ToString("N0"));
                });

                if (payment.Method == PaymentMethod.Cash)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem(1).Text("  Recibido:").FontSize(7).FontColor(Colors.Grey.Darken2);
                        row.RelativeItem(1).AlignRight().Text((payment.AmountTendered ?? payment.Amount).ToString("N0")).FontSize(7).FontColor(Colors.Grey.Darken2);
                    });
                    column.Item().Row(row =>
                    {
                        row.RelativeItem(1).Text("  Cambio:").FontSize(7).FontColor(Colors.Grey.Darken2);
                        row.RelativeItem(1).AlignRight().Text((payment.Change ?? 0m).ToString("N0")).FontSize(7).FontColor(Colors.Grey.Darken2);
                    });
                }
            }
        });
    }

    private static void ComposeTaxSummaryTable(IContainer container, Sale sale)
    {
        var groups = GroupTaxes(sale);

        if (groups.Count == 0)
        {
            container.Height(0);
            return;
        }

        container.Column(column =>
        {
            column.Item().AlignCenter().Text("RESUMEN DE IMPUESTOS").FontSize(9).Bold();

            column.Item().PaddingTop(1, Unit.Millimetre).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).Text("IMPUESTO").Bold();
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).AlignRight().Text("BASE").Bold();
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).AlignRight().Text("TARIFA").Bold();
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).AlignRight().Text("IMPUESTO").Bold();
                });

                // Incluye tipos de impuesto cuyo monto resultó en $0 (ej. una tarifa al
                // 0%) — no se filtran, se listan todos los aplicados a esta venta.
                foreach (var group in groups)
                {
                    table.Cell().PaddingTop(1, Unit.Millimetre).Text(group.TaxTypeName);
                    table.Cell().PaddingTop(1, Unit.Millimetre).AlignRight().Text(group.Base.ToString("N0"));
                    table.Cell().PaddingTop(1, Unit.Millimetre).AlignRight().Text($"{group.Percentage:0.####}%");
                    table.Cell().PaddingTop(1, Unit.Millimetre).AlignRight().Text(group.Amount.ToString("N0"));
                }
            });

            column.Item().PaddingTop(1, Unit.Millimetre).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
        });
    }

    // Un mismo tipo de impuesto puede aparecer en varias líneas con el mismo
    // porcentaje; se agrupa por (nombre, porcentaje) para mostrar un único
    // renglón por impuesto. Misma lógica que GetSaleTaxSummaryQueryHandler.
    private static List<TaxGroup> GroupTaxes(Sale sale)
    {
        return sale.Items
            .SelectMany(i => i.Taxes.Select(t => new { i.SubtotalBase, Tax = t }))
            .GroupBy(x => new { x.Tax.TaxTypeName, x.Tax.Percentage })
            .OrderBy(g => g.Key.TaxTypeName)
            .ThenBy(g => g.Key.Percentage)
            .Select(g => new TaxGroup(g.Key.TaxTypeName, g.Key.Percentage, g.Sum(x => x.SubtotalBase), g.Sum(x => x.Tax.Amount)))
            .ToList();
    }

    private sealed record TaxGroup(string TaxTypeName, decimal Percentage, decimal Base, decimal Amount);

    private static string DescribePaymentMethod(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.Transfer => "Transferencia",
        PaymentMethod.Credit => "Crédito / Fiado",
        _ => method.ToString()
    };

    private static void ComposeFooter(IContainer container, TenantConfig config)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Text("¡Gracias por su compra!").FontSize(9).Bold();
            column.Item().AlignCenter().Text("¡Vuelve pronto!").FontSize(8);

            column.Item().PaddingTop(2, Unit.Millimetre).LineHorizontal(1).LineColor(Colors.Grey.Darken1);

            column.Item().PaddingTop(1, Unit.Millimetre).AlignCenter().Text("Software desarrollado por").FontSize(6).FontColor(Colors.Grey.Darken2);
            column.Item().AlignCenter().Text("AXON POS").FontSize(7).Bold();

            if (!string.IsNullOrWhiteSpace(config.Website))
            {
                column.Item().AlignCenter().Text(config.Website).FontSize(6).FontColor(Colors.Grey.Darken2);
            }
        });
    }
}
