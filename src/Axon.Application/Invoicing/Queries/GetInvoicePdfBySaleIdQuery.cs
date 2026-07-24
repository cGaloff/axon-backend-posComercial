using MediatR;

namespace Axon.Application.Invoicing.Queries;

// A diferencia de GET /api/sales/{id}/receipt (reimpresión genérica que
// regenera el PDF desde la venta sin importar si ya se facturó), este query
// exige que exista una Invoice real para la venta — es "ver la factura de esa
// venta desde el historial de ventas", no un recibo provisional.
public record GetInvoicePdfBySaleIdQuery(Guid SaleId) : IRequest<byte[]>;
