using Axon.Application.Common.Models;
using MediatR;

namespace Axon.Application.Invoicing.Queries;

// From/To llevan fecha Y hora (no solo fecha): se interpretan como el rango en
// hora Colombia y se convierten a UTC antes de filtrar contra Invoice.IssuedAt
// (mismo criterio ya usado en los reportes de ventas, ver ColombiaTime.ToUtc).
public record GetInvoicesQuery(
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<InvoiceDto>>;

public record InvoiceDto(
    Guid Id,
    long Number,
    DateTime IssuedAt,
    Guid SaleId,
    string SaleNumber,
    string CustomerName,
    decimal Total,
    List<InvoiceItemDto> Items,
    List<InvoicePaymentDto> Payments);

public record InvoiceItemDto(
    Guid ProductId,
    string ProductName,
    string ProductSku,
    decimal UnitPrice,
    int Quantity,
    decimal Discount,
    decimal Subtotal,
    decimal SubtotalBase,
    List<InvoiceItemTaxDto> Taxes);

public record InvoiceItemTaxDto(Guid TaxTypeId, string TaxTypeName, decimal Percentage, decimal Amount);

public record InvoicePaymentDto(string Method, decimal Amount, decimal? AmountTendered, decimal? Change);
