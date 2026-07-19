using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.TenantConfig.Queries;

public class GetSaleTaxSummaryQueryHandler : IRequestHandler<GetSaleTaxSummaryQuery, SaleTaxSummaryDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetSaleTaxSummaryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SaleTaxSummaryDto> Handle(GetSaleTaxSummaryQuery request, CancellationToken cancellationToken)
    {
        var sale = await _dbContext.Sales
            .Include(s => s.Items)
            .SingleOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale is null)
        {
            throw new DomainException("La venta no existe");
        }

        var subtotalBase = sale.Items.Sum(i => i.SubtotalBase);
        var totalTax = sale.Items.Sum(i => i.TaxAmount);
        var discount = sale.Items.Sum(i => i.Discount);

        var breakdown = sale.Items
            .GroupBy(i => i.TaxPercentage)
            .Select(g => new TaxBreakdownDto(g.Key, g.Sum(i => i.SubtotalBase), g.Sum(i => i.TaxAmount)))
            .OrderBy(b => b.Rate)
            .ToList();

        return new SaleTaxSummaryDto(subtotalBase, totalTax, subtotalBase + totalTax, discount, breakdown);
    }
}
