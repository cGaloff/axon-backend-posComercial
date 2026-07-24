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
        var totalTax = sale.Items.Sum(i => i.TotalTaxAmount);
        var discount = sale.Items.Sum(i => i.Discount);

        // Cada impuesto de cada línea comparte la base gravable de esa línea (todos
        // los impuestos se calculan sobre la misma base, no se componen entre sí).
        var breakdown = sale.Items
            .SelectMany(i => i.Taxes.Select(t => new { i.SubtotalBase, Tax = t }))
            .GroupBy(x => new { x.Tax.TaxTypeId, x.Tax.TaxTypeName, x.Tax.Percentage })
            .Select(g => new TaxBreakdownDto(
                g.Key.TaxTypeId,
                g.Key.TaxTypeName,
                g.Key.Percentage,
                g.Sum(x => x.SubtotalBase),
                g.Sum(x => x.Tax.Amount)))
            .OrderBy(b => b.TaxTypeName)
            .ThenBy(b => b.Rate)
            .ToList();

        return new SaleTaxSummaryDto(subtotalBase, totalTax, subtotalBase + totalTax, discount, breakdown);
    }
}
