using Axon.Domain.Entities.Sales;

namespace Axon.API.DTOs.Sales;

// Discount y DiscountPercentage son mutuamente excluyentes: un monto fijo
// para este producto, o un % que el backend convierte al monto equivalente.
public record SaleItemRequestDto(Guid ProductId, int Quantity, decimal? Discount = null, decimal? DiscountPercentage = null);

// PaymentMethod: catálogo heredado (Cash/Card/Transfer/Credit), pendiente de
// confirmar con negocio para el caso de pagos divididos (ver Axon.Application.Sales.Commands.SalePaymentRequest).
public record SalePaymentRequestDto(PaymentMethod Method, decimal Amount, decimal? AmountTendered = null);

public record ProcessSaleRequest(
    List<SaleItemRequestDto> Items,
    List<SalePaymentRequestDto> Payments,
    Guid CashRegisterId,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerEmail,
    string? Notes,
    CustomerDocumentType? CustomerDocumentType = null,
    string? CustomerDocumentNumber = null,
    decimal? SaleDiscountAmount = null,
    decimal? SaleDiscountPercentage = null);
