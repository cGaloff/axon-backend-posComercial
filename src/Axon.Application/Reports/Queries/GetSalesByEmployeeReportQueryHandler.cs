using Axon.Application.Interfaces;
using Axon.Domain.Entities.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Reports.Queries;

public class GetSalesByEmployeeReportQueryHandler : IRequestHandler<GetSalesByEmployeeReportQuery, SalesByEmployeeReportDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetSalesByEmployeeReportQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SalesByEmployeeReportDto> Handle(GetSalesByEmployeeReportQuery request, CancellationToken cancellationToken)
    {
        // FromDate/ToDate llegan como límites del día calendario en hora Colombia;
        // mismo criterio que el resto de reportes (ver GetSalesSummaryReportQueryHandler).
        var fromUtc = ColombiaTime.ToUtc(request.FromDate);
        var toUtc = ColombiaTime.ToUtc(request.ToDate);

        // Solo ventas completadas cuentan como venta real del empleado; anuladas/
        // devueltas no representan una venta efectiva en su desempeño.
        var sales = await _dbContext.Sales
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt <= toUtc && s.Status == SaleStatus.Completed)
            .Select(s => new { s.CreatedBy, s.Total })
            .ToListAsync(cancellationToken);

        if (sales.Count == 0)
        {
            return new SalesByEmployeeReportDto(new List<EmployeeSalesDto>());
        }

        var userIds = sales.Select(s => s.CreatedBy).Distinct().ToList();

        var userNames = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var employees = sales
            .GroupBy(s => s.CreatedBy)
            .Select(g => new EmployeeSalesDto(
                g.Key,
                userNames.GetValueOrDefault(g.Key, "(usuario no encontrado)"),
                g.Count(),
                g.Sum(s => s.Total)))
            .OrderByDescending(e => e.TotalRevenue)
            .ToList();

        return new SalesByEmployeeReportDto(employees);
    }
}
