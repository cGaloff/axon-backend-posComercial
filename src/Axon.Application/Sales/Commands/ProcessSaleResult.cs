using Axon.Domain.Entities.Sales;

namespace Axon.Application.Sales.Commands;

// PdfReceipt/InvoiceNumber siempre vienen con valor: toda venta presencial
// completa y factura de inmediato sin importar el método de pago (ver
// Sale.AddPayment). Quedan nullable por si en el futuro se integra una
// pasarela de pago real que deje una venta PendingPayment.
public record ProcessSaleResult(
    Guid SaleId,
    string SaleNumber,
    decimal Total,
    decimal TotalChange,
    SaleStatus Status,
    long? InvoiceNumber,
    byte[]? PdfReceipt);
