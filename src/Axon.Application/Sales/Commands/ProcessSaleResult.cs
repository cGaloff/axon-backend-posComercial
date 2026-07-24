using Axon.Domain.Entities.Sales;

namespace Axon.Application.Sales.Commands;

// PdfReceipt/InvoiceNumber son null cuando la venta queda PendingPayment (pago
// con tarjeta/transferencia aún no confirmado): la factura se emite después,
// al confirmarse el pago (ver ConfirmSalePaymentCommandHandler).
public record ProcessSaleResult(
    Guid SaleId,
    string SaleNumber,
    decimal Total,
    decimal TotalChange,
    SaleStatus Status,
    long? InvoiceNumber,
    byte[]? PdfReceipt);
