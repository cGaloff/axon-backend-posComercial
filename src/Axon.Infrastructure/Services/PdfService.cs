using System.Reflection;
using Axon.Domain.Entities;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Interfaces;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Axon.Infrastructure.Services;

public class PdfService : IPdfService
{
    private const float ReceiptWidthMm = 76;
    private const float MarginMm = 3;

    // Fuente monoespaciada (Cascadia Mono, de Microsoft, licencia SIL OFL —
    // libre para incrustar/redistribuir) embebida como recurso: así el recibo
    // se ve exactamente igual sin importar qué fuentes tenga instaladas el
    // sistema operativo donde corra (el contenedor Linux de producción no
    // trae ninguna fuente propia). El alineado en columnas de un recibo
    // depende de que cada carácter/dígito ocupe el mismo ancho.
    private const string FontFamilyName = "Axon Receipt Mono";

    // QuestPDF no soporta líneas punteadas nativas (LineHorizontal solo dibuja
    // trazo continuo), así que el separador "- - - - -" se dibuja como texto:
    // cada guion ocupa el mismo ancho gracias a la fuente monoespaciada de
    // arriba, resultando en un patrón parejo sin importar dónde se use.
    private static readonly string DashedLine = string.Join(" ", Enumerable.Repeat("-", 21));

    private static readonly TimeZoneInfo ColombiaTimeZone = ResolveColombiaTimeZone();

    static PdfService()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("CascadiaMono.ttf", StringComparison.OrdinalIgnoreCase));

        using var fontStream = assembly.GetManifestResourceStream(resourceName)!;
        FontManager.RegisterFontWithCustomName(FontFamilyName, fontStream);
    }

    public byte[] GenerateSaleReceipt(Sale sale, TenantConfig config, string cashierName)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(ReceiptWidthMm, Unit.Millimetre);
                page.Margin(MarginMm, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontFamily(FontFamilyName).FontSize(8).FontColor("#222222"));

                page.Content().Column(column =>
                {
                    column.Item().Element(c => ComposeHeader(c, config));
                    column.Item().PaddingTop(2, Unit.Millimetre).Element(c => ComposeSaleData(c, sale, cashierName));
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
            column.Item().AlignCenter().Text(config.BusinessName.ToUpperInvariant()).FontSize(13).Bold();

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

            /* if (!string.IsNullOrWhiteSpace(config.Email))
            {
                column.Item().AlignCenter().Text(config.Email).FontSize(7);
            }

            if (!string.IsNullOrWhiteSpace(config.Website))
            {
                column.Item().AlignCenter().Text(config.Website).FontSize(7);
            }
            */
            column.Item().PaddingTop(2, Unit.Millimetre).Text(DashedLine).FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void ComposeSaleData(IContainer container, Sale sale, string cashierName)
    {
        container.Column(column =>
        {
            var localCreatedAt = ToColombiaTime(sale.CreatedAt);

            // Formato de número de factura (SaleNumber, ej. "VTA-20260726-4CD6D0"):
            // se deja tal cual — es una decisión de negocio pendiente de confirmar
            // (consecutivo simple vs. el formato actual), no un bug de este cambio.
            column.Item().AlignCenter().Text($"FACTURA POS No. {sale.SaleNumber}").FontSize(11).Bold();

            column.Item().PaddingVertical(1, Unit.Millimetre).Text(DashedLine).FontSize(8).FontColor(Colors.Grey.Lighten1);

            column.Item().Text($"Fecha: {localCreatedAt:dd/MM/yyyy}   Hora: {localCreatedAt:HH:mm}");
            column.Item().Text($"Cajero: {cashierName}");

            // Consumidor Final ya queda resuelto por Sale.Create (ver
            // Sale.DefaultCustomerName); el fallback aquí es solo defensivo para
            // ventas históricas anteriores a este cambio que puedan tener el campo
            // en blanco. El documento (C.C./NIT), en cambio, NO tiene default: si
            // el cliente no lo dio, la línea queda vacía a propósito.
            column.Item().Text($"Cliente: {(string.IsNullOrWhiteSpace(sale.CustomerName) ? Sale.DefaultCustomerName : sale.CustomerName)}");
            column.Item().Text($"C.C./NIT: {sale.CustomerDocumentNumber}");

            column.Item().PaddingTop(2, Unit.Millimetre).Text(DashedLine).FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void ComposeItemsTable(IContainer container, Sale sale)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                // Proporciones (en mm, ya que CANT+PRODUCTO+PRECIO+TOTAL suma
                // exactamente los 70mm de ancho útil): PRECIO y TOTAL ensanchados
                // para que quepan montos de hasta 9 dígitos ("9.999.999", un
                // celular o electrodoméstico) sin partirse a dos líneas — antes
                // (13/15mm) alcanzaba para montos de 6 cifras pero no de 7+. TOTAL
                // además debe alcanzar para el monto en negrita más grande de
                // "TOTAL A PAGAR" (10pt, más ancho por carácter que el resto del
                // recibo a 8pt — ver ComposeTotals).
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(8);
                    columns.RelativeColumn(27);
                    columns.RelativeColumn(16);
                    columns.RelativeColumn(19);
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).AlignCenter().Text("CANT").Bold();
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).Text("PRODUCTO").Bold();
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).AlignRight().Text("PRECIO").Bold();
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).AlignRight().Text("TOTAL").Bold();
                });

                foreach (var item in sale.Items)
                {
                    table.Cell().PaddingTop(1, Unit.Millimetre).AlignCenter().Text(item.Quantity.ToString());
                    table.Cell().PaddingTop(1, Unit.Millimetre).Text(item.ProductName);
                    table.Cell().PaddingTop(1, Unit.Millimetre).AlignRight().Text(item.UnitPrice.ToString("N0"));
                    table.Cell().PaddingTop(1, Unit.Millimetre).AlignRight().Text(item.Subtotal.ToString("N0"));

                    // El monto de cada impuesto/descuento queda alineado bajo la
                    // columna TOTAL (no como un solo texto suelto) para que los
                    // porcentajes se lean en la misma columna que el resto de montos.
                    // La celda vacía inicial deja la etiqueta arrancando exactamente
                    // donde arranca el nombre del producto (columna PRODUCTO), no bajo
                    // CANT como antes.
                    foreach (var tax in item.Taxes)
                    {
                        table.Cell();
                        table.Cell().ColumnSpan(2).Text($"{tax.TaxTypeName} {tax.Percentage:0.####}%").FontSize(7).FontColor(Colors.Grey.Darken2);
                        table.Cell().AlignRight().Text(tax.Amount.ToString("N0")).FontSize(7).FontColor(Colors.Grey.Darken2);
                    }

                    if (item.Discount > 0)
                    {
                        // Si el descuento se dio como % (no como monto fijo), se
                        // muestra junto a la etiqueta — igual que "Descuento
                        // general (10%)" en Totales.
                        var discountLabel = item.DiscountPercentage.HasValue
                            ? $"Desc: ({item.DiscountPercentage.Value:0.##}%)"
                            : "Desc:";

                        table.Cell();
                        table.Cell().ColumnSpan(2).Text(discountLabel).FontSize(7).FontColor(Colors.Grey.Darken2);
                        table.Cell().AlignRight().Text($"-{item.Discount:N0}").FontSize(7).FontColor(Colors.Grey.Darken2);
                    }
                }
            });

            column.Item().PaddingTop(1, Unit.Millimetre).Text(DashedLine).FontSize(8).FontColor(Colors.Grey.Darken1);
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

            // Proporción 51:19, igual que "CANT+PRODUCTO+PRECIO : TOTAL" en la
            // tabla de items (8+27+16 : 19) — así el borde derecho de los montos
            // queda alineado en todas las secciones del recibo, no solo dentro de
            // cada una por separado. El TOTAL necesita más espacio del que parece
            // porque "TOTAL A PAGAR" se imprime más grande (10pt) que el resto de
            // las líneas (8pt).
            void TotalLine(string label, decimal amount, bool bold = false)
            {
                column.Item().Row(row =>
                {
                    var labelText = row.RelativeItem(51).Text(label).FontSize(bold ? 10 : 8);
                    var amountText = row.RelativeItem(19).AlignRight().Text(amount.ToString("N0")).FontSize(bold ? 10 : 8);

                    if (bold)
                    {
                        labelText.Bold();
                        amountText.Bold();
                    }
                });
            }

            TotalLine("Subtotal", grossSubtotal);
            TotalLine("Descuento", -totalDiscount);

            // Aparte de los descuentos por producto (línea "Descuento" de arriba):
            // un solo descuento aplicado a TODA la venta, sin repartirlo
            // visualmente por producto aunque internamente sí se repartió para
            // recalcular el impuesto de cada línea (ver SaleItem.GeneralDiscountShare).
            // Si se dio como % (no como monto fijo), se muestra junto al monto.
            if (sale.GeneralDiscountAmount > 0)
            {
                var generalDiscountLabel = sale.GeneralDiscountPercentage.HasValue
                    ? $"Descuento general ({sale.GeneralDiscountPercentage.Value:0.##}%)"
                    : "Descuento general";

                TotalLine(generalDiscountLabel, -sale.GeneralDiscountAmount);
            }

            TotalLine("Base Gravable", subtotalBase);
            TotalLine("Impuestos", totalTax);

            column.Item().PaddingTop(1, Unit.Millimetre).Text(DashedLine).FontSize(8).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(1, Unit.Millimetre).Element(c => { });

            TotalLine("TOTAL A PAGAR", sale.Total, bold: true);

            column.Item().PaddingTop(1, Unit.Millimetre).Text(DashedLine).FontSize(8).FontColor(Colors.Grey.Darken1);

            // Pagos divididos: una línea por método (método + monto en la misma
            // fila, sin desglosar cálculos internos); el efectivo además muestra lo
            // entregado y el vuelto de esa línea puntual (no aplica a tarjeta/
            // transferencia, que se cobran por el monto exacto).
            foreach (var payment in sale.Payments)
            {
                column.Item().PaddingTop(1, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem(51).Text($"Forma de Pago: {DescribePaymentMethod(payment.Method)}");
                    row.RelativeItem(19).AlignRight().Text(payment.Amount.ToString("N0"));
                });

                if (payment.Method == PaymentMethod.Cash)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem(51).Text("  Recibido:").FontSize(7).FontColor(Colors.Grey.Darken2);
                        row.RelativeItem(19).AlignRight().Text((payment.AmountTendered ?? payment.Amount).ToString("N0")).FontSize(7).FontColor(Colors.Grey.Darken2);
                    });
                    column.Item().Row(row =>
                    {
                        row.RelativeItem(51).Text("  Cambio:").FontSize(7).FontColor(Colors.Grey.Darken2);
                        row.RelativeItem(19).AlignRight().Text((payment.Change ?? 0m).ToString("N0")).FontSize(7).FontColor(Colors.Grey.Darken2);
                    });
                }
            }
        });
    }

    // Categorías fijas del negocio (siempre se muestran, aunque sea en $0):
    // IVA 19%, IVA 5%, Impoconsumo (= tipo "INC" del catálogo), Exento (= IVA al
    // 0%) y Excluido de IVA (= base de los productos que no tienen NINGUNA línea
    // de IVA, ej. canasta básica). Cualquier otro impuesto realmente usado en la
    // venta (IVA a otra tarifa, GMF, ICA, INC PL, Incombustible, Incarbono, IBUA)
    // se agrega como fila adicional debajo, solo si aplica — así el resumen
    // siempre cuadra con el total de impuestos de la venta.
    private static void ComposeTaxSummaryTable(IContainer container, Sale sale)
    {
        var groups = GroupTaxes(sale);

        decimal AmountFor(string taxTypeName, decimal? percentage) => groups
            .Where(g => g.TaxTypeName == taxTypeName && (percentage == null || g.Percentage == percentage.Value))
            .Sum(g => g.Amount);

        var iva19 = AmountFor("IVA", 19m);
        var iva5 = AmountFor("IVA", 5m);
        var impoconsumo = AmountFor("INC", null);
        var exento = AmountFor("IVA", 0m);

        var excluidoDeIva = sale.Items
            .Where(i => !i.Taxes.Any(t => t.TaxTypeName == "IVA"))
            .Sum(i => i.SubtotalBase);

        var extraGroups = groups
            .Where(g => g.TaxTypeName != "INC" && !(g.TaxTypeName == "IVA" && (g.Percentage == 19m || g.Percentage == 5m || g.Percentage == 0m)))
            .ToList();

        container.Column(column =>
        {
            column.Item().AlignCenter().Text("RESUMEN DE IMPUESTOS").FontSize(9).Bold();

            column.Item().PaddingTop(1, Unit.Millimetre).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(51);
                    columns.RelativeColumn(19);
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).Text("IMPUESTO").Bold();
                    header.Cell().BorderBottom(0.75f).PaddingBottom(1, Unit.Millimetre).AlignRight().Text("VALOR").Bold();
                });

                void Row(string label, decimal amount)
                {
                    table.Cell().PaddingTop(1, Unit.Millimetre).Text(label);
                    table.Cell().PaddingTop(1, Unit.Millimetre).AlignRight().Text(amount.ToString("N0"));
                }

                Row("IVA 19%", iva19);
                Row("IVA 5%", iva5);
                Row("Impoconsumo", impoconsumo);
                Row("Exento", exento);
                Row("Excluido de IVA", excluidoDeIva);

                foreach (var group in extraGroups)
                {
                    Row($"{group.TaxTypeName} {group.Percentage:0.####}%", group.Amount);
                }
            });

            column.Item().PaddingTop(1, Unit.Millimetre).Text(DashedLine).FontSize(8).FontColor(Colors.Grey.Darken1);
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

            column.Item().PaddingTop(2, Unit.Millimetre).Text(DashedLine).FontSize(8).FontColor(Colors.Grey.Darken1);

            column.Item().PaddingTop(1, Unit.Millimetre).AlignCenter().Text("Software de facturación: ").FontSize(6).FontColor(Colors.Grey.Darken2);
            column.Item().AlignCenter().Text("AXON COMPANY - NIT: 901996326-8").FontSize(9).Bold();
            column.Item().AlignCenter().Text("www.axoncompanys.com.co").FontSize(6).FontColor(Colors.Grey.Darken2);
            /*
            if (!string.IsNullOrWhiteSpace(config.Website))
            {
                column.Item().AlignCenter().Text(config.Website).FontSize(6).FontColor(Colors.Grey.Darken2);
            }
            */
        });
    }
}
