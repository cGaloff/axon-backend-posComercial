using MediatR;

namespace Axon.Application.TenantConfig.Queries;

public record GetSaleTaxSummaryQuery(Guid SaleId) : IRequest<SaleTaxSummaryDto>;

public record SaleTaxSummaryDto(
    decimal SubtotalBase,
    decimal TotalTax,
    decimal Total,
    decimal Discount,
    List<TaxBreakdownDto> TaxBreakdown);

public record TaxBreakdownDto(decimal Rate, decimal Base, decimal Amount);
