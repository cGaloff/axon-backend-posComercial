using MediatR;

namespace Axon.Application.Invoicing.Commands;

// Dispara, a partir de una venta ya completada, las dos salidas del mismo
// evento de "pago exitoso": el PDF de recibo (reutilizando IPdfService) y la
// persistencia del registro Invoice para consulta posterior. Se invoca desde
// ProcessSaleCommandHandler (venta completada de inmediato) y desde
// ConfirmSalePaymentCommandHandler (venta que pasa de pendiente a completada
// tras confirmación de pago) — el mismo comando, dos puntos de entrada.
public record IssueInvoiceCommand(Guid SaleId) : IRequest<IssueInvoiceResult>;

public record IssueInvoiceResult(Guid InvoiceId, long Number, byte[] PdfReceipt);
