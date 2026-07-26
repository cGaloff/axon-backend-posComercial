using Axon.Application.Common.Models;
using Axon.Application.Interfaces;
using Axon.Application.Reports;
using Axon.Application.Sales.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Sales.Queries;

public class GetSalesHistoryQueryHandler : IRequestHandler<GetSalesHistoryQuery, PagedResult<SaleDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetSalesHistoryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<SaleDto>> Handle(GetSalesHistoryQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Sales.AsQueryable();

        // From/To representan el día calendario en hora Colombia (no UTC), así
        // que se truncan a la fecha y se expanden al día completo antes de
        // convertir a UTC: si no se trunca, un filtro de un solo día (from == to)
        // cubriría un rango de 0 segundos y no mostraría ninguna venta.
        if (request.From.HasValue)
        {
            var fromDate = ColombiaTime.ToUtc(request.From.Value.Date);
            query = query.Where(s => s.CreatedAt >= fromDate);
        }

        if (request.To.HasValue)
        {
            var toDate = ColombiaTime.ToUtc(request.To.Value.Date.AddDays(1).AddTicks(-1));
            query = query.Where(s => s.CreatedAt <= toDate);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Status.Value);
        }

        if (request.CustomerId.HasValue)
        {
            query = query.Where(s => s.CustomerId == request.CustomerId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new SaleDto(
                s.Id,
                s.SaleNumber,
                s.CustomerName,
                s.Status.ToString(),
                s.Total,
                s.CreatedAt,
                s.Items.Count,
                s.Payments
                    .Select(p => new SalePaymentDto(p.Id, p.Method.ToString(), p.Amount, p.AmountTendered, p.Change))
                    .ToList(),
                // Null si la venta aún no tiene factura emitida (p. ej. pago con
                // tarjeta/transferencia todavía pendiente de confirmación).
                _dbContext.Invoices
                    .Where(i => i.SaleId == s.Id)
                    .Select(i => (long?)i.Number)
                    .SingleOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedResult<SaleDto>(totalCount, request.Page, request.PageSize, items);
    }
}
