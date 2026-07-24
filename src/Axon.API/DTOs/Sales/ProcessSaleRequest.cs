using Axon.Domain.Entities.Sales;

namespace Axon.API.DTOs.Sales;

public record SaleItemRequestDto(Guid ProductId, int Quantity, decimal Discount = 0);

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
    string? Notes);
