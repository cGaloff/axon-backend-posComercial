namespace Axon.Application.Sales.DTOs;

public record SaleDto(
    Guid Id,
    string SaleNumber,
    string? CustomerName,
    string PaymentMethod,
    string Status,
    decimal Total,
    DateTime CreatedAt,
    int ItemCount);
