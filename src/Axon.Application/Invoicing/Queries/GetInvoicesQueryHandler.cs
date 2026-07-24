using Axon.Application.Common.Models;
using Axon.Application.Interfaces;
using Axon.Application.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Invoicing.Queries;

public class GetInvoicesQueryHandler : IRequestHandler<GetInvoicesQuery, PagedResult<InvoiceDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetInvoicesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<InvoiceDto>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Invoices.AsQueryable();

        // From/To incluyen hora (no solo fecha): se asumen en hora Colombia y se
        // convierten a UTC antes de comparar contra IssuedAt, mismo criterio que
        // en los reportes de ventas (evita el mismo bug de zona horaria del prompt 2).
        if (request.From.HasValue)
        {
            var fromUtc = ColombiaTime.ToUtc(request.From.Value);
            query = query.Where(i => i.IssuedAt >= fromUtc);
        }

        if (request.To.HasValue)
        {
            var toUtc = ColombiaTime.ToUtc(request.To.Value);
            query = query.Where(i => i.IssuedAt <= toUtc);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.Number)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new InvoiceDto(
                i.Id,
                i.Number,
                i.IssuedAt,
                i.SaleId,
                i.SaleNumber,
                i.CustomerName,
                i.Total,
                i.Items.Select(x => new InvoiceItemDto(
                    x.ProductId,
                    x.ProductName,
                    x.ProductSku,
                    x.UnitPrice,
                    x.Quantity,
                    x.Discount,
                    x.Subtotal,
                    x.SubtotalBase,
                    x.Taxes.Select(t => new InvoiceItemTaxDto(t.TaxTypeId, t.TaxTypeName, t.Percentage, t.Amount)).ToList()))
                    .ToList(),
                i.Payments.Select(p => new InvoicePaymentDto(p.Method.ToString(), p.Amount, p.AmountTendered, p.Change)).ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<InvoiceDto>(totalCount, request.Page, request.PageSize, items);
    }
}
