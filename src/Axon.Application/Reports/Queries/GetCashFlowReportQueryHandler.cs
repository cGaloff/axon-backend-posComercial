using System.Globalization;
using Axon.Application.Interfaces;
using Axon.Domain.Entities.CashRegister;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Reports.Queries;

public class GetCashFlowReportQueryHandler : IRequestHandler<GetCashFlowReportQuery, CashFlowReportDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetCashFlowReportQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CashFlowReportDto> Handle(GetCashFlowReportQuery request, CancellationToken cancellationToken)
    {
        var movements = await _dbContext.CashMovements
            .Where(m => m.CreatedAt >= request.FromDate && m.CreatedAt <= request.ToDate)
            .ToListAsync(cancellationToken);

        // Los pagos a proveedor no tocan la caja física (ver ProcessSaleCommandHandler/
        // RegisterSupplierPaymentCommandHandler), así que no viven en cash_movements;
        // se agregan aquí como egreso de negocio, tal como sugiere el requerimiento.
        var supplierPayments = await _dbContext.SupplierPayments
            .Where(p => p.PaidAt >= request.FromDate && p.PaidAt <= request.ToDate)
            .ToListAsync(cancellationToken);

        var points = new List<(DateTime Date, decimal Income, decimal Expense)>();

        foreach (var movement in movements)
        {
            var income = movement.Type is CashMovementType.CashSale or CashMovementType.CreditSale
                or CashMovementType.CardSale or CashMovementType.TransferSale or CashMovementType.ManualIncome
                ? movement.Amount
                : 0m;

            var expense = movement.Type == CashMovementType.Expense ? movement.Amount : 0m;

            if (income > 0 || expense > 0)
            {
                points.Add((ColombiaTime.ToLocal(movement.CreatedAt), income, expense));
            }
        }

        foreach (var payment in supplierPayments)
        {
            points.Add((ColombiaTime.ToLocal(payment.PaidAt), 0m, payment.Amount));
        }

        var series = points
            .GroupBy(p => GetDateLabel(p.Date, request.GroupBy))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new CashFlowDataPointDto(g.Key, g.Sum(x => x.Income), g.Sum(x => x.Expense)))
            .ToList();

        var totalIncome = points.Sum(p => p.Income);
        var totalExpense = points.Sum(p => p.Expense);

        return new CashFlowReportDto(totalIncome, totalExpense, totalIncome - totalExpense, series);
    }

    private static string GetDateLabel(DateTime date, string groupBy)
    {
        return groupBy switch
        {
            "Week" => $"{ISOWeek.GetYear(date)}-W{ISOWeek.GetWeekOfYear(date):D2}",
            "Month" => date.ToString("yyyy-MM"),
            _ => date.ToString("yyyy-MM-dd")
        };
    }
}
