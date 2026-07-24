using MediatR;

namespace Axon.Application.TenantConfig.Queries;

public record GetSaleTaxSummaryQuery(Guid SaleId) : IRequest<SaleTaxSummaryDto>;

public record SaleTaxSummaryDto(
    decimal SubtotalBase,
    decimal TotalTax,
    decimal Total,
    decimal Discount,
    List<TaxBreakdownDto> TaxBreakdown);

public record TaxBreakdownDto(Guid TaxTypeId, string TaxTypeName, decimal Rate, decimal Base, decimal Amount);
