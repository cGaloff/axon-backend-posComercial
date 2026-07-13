using Axon.Domain.Entities.Sales;

namespace Axon.API.DTOs.Sales;

public record SaleItemRequestDto(Guid ProductId, int Quantity, decimal Discount = 0);

public record ProcessSaleRequest(
    List<SaleItemRequestDto> Items,
    PaymentMethod PaymentMethod,
    Guid CashRegisterId,
    Guid CreatedBy,
    decimal AmountPaid,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerEmail,
    string? Notes);
