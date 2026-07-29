using Axon.Domain.Entities.Sales;

namespace Axon.Application.Sales.DTOs;

public record SaleDto(
    Guid Id,
    string SaleNumber,
    string? CustomerName,
    CustomerDocumentType? CustomerDocumentType,
    string? CustomerDocumentNumber,
    string Status,
    decimal Total,
    DateTime CreatedAt,
    int ItemCount,
    List<SalePaymentDto> Payments,
    long? InvoiceNumber);

public record SalePaymentDto(
    Guid Id,
    string Method,
    decimal Amount,
    decimal? AmountTendered,
    decimal? Change);
