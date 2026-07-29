using Axon.Domain.Entities.Sales;
using MediatR;

namespace Axon.Application.Sales.Commands;

// Discount y DiscountPercentage son mutuamente excluyentes (validados en
// ProcessSaleCommandValidator): un monto fijo para este producto puntual, o un
// % que el handler convierte al monto equivalente sobre el precio de este
// mismo producto (no sobre toda la venta — para eso existe SaleDiscountAmount/
// SaleDiscountPercentage más abajo).
public record SaleItemRequest(Guid ProductId, int Quantity, decimal? Discount = null, decimal? DiscountPercentage = null);

// PaymentMethod: catálogo heredado del modelo anterior (Cash/Card/Transfer/Credit).
// PENDIENTE DE CONFIRMAR CON NEGOCIO si este es el catálogo real de métodos de
// pago que se deben poder combinar en una misma venta (ver resumen del prompt 4).
public record SalePaymentRequest(PaymentMethod Method, decimal Amount, decimal? AmountTendered = null);

public record ProcessSaleCommand(
    List<SaleItemRequest> Items,
    List<SalePaymentRequest> Payments,
    Guid CashRegisterId,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerEmail,
    string? Notes,
    CustomerDocumentType? CustomerDocumentType = null,
    string? CustomerDocumentNumber = null,
    // Descuento a TODA la venta (además de los descuentos por producto en
    // Items[].Discount), mutuamente excluyente: monto fijo o porcentaje, no
    // ambos. Se reparte proporcionalmente entre los ítems en el handler.
    decimal? SaleDiscountAmount = null,
    decimal? SaleDiscountPercentage = null) : IRequest<ProcessSaleResult>;
