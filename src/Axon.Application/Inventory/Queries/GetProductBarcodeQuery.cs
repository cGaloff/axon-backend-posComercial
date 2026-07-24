using MediatR;

namespace Axon.Application.Inventory.Queries;

// Bajo demanda, no persistido: mismo criterio que el recibo PDF de venta
// (GetSaleReceiptQuery) — el contenido (Sku) ya existe, así que la imagen se
// genera al pedirse en vez de guardarse como blob en la base de datos.
public record GetProductBarcodeQuery(Guid ProductId) : IRequest<byte[]>;
