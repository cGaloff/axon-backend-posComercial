using System.Reflection;
using Axon.Application.Reports.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.CashRegister;
using CashRegisterEntity = Axon.Domain.Entities.CashRegister.CashRegister;

namespace Axon.Application.Tests.Reports;

public class GetCashFlowReportQueryHandlerTests
{
    private static void SetCreatedAt(CashMovement movement, DateTime createdAtUtc)
    {
        typeof(CashMovement).GetProperty(nameof(CashMovement.CreatedAt), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(movement, createdAtUtc);
    }

    // Mismo bug que el resto de reportes: un filtro de un solo día (from == to,
    // ambos a medianoche) no mostraba movimientos, porque el rango resultante
    // medianoche-a-medianoche cubre 0 segundos, y además FromDate/ToDate
    // representan el día calendario en hora Colombia, no UTC.
    [Fact]
    public async Task Handle_WithSingleDayFilter_IncludesMovementsFromThatDayInColombiaTime()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var register = CashRegisterEntity.Create("Caja Principal", "", isDefault: false);
        var session = CashSession.Create(register.Id, userId, 0m);
        dbContext.CashRegisters.Add(register);
        dbContext.CashSessions.Add(session);

        var movement = CashMovement.Create(session.Id, CashMovementType.CashSale, 50000m, "Venta de prueba", userId);
        // 24/07/2026 10:00 hora Colombia (UTC-5) == 24/07/2026 15:00 UTC.
        SetCreatedAt(movement, new DateTime(2026, 7, 24, 15, 0, 0, DateTimeKind.Utc));
        dbContext.CashMovements.Add(movement);

        await dbContext.SaveChangesAsync();

        var handler = new GetCashFlowReportQueryHandler(dbContext);

        var query = new GetCashFlowReportQuery(
            FromDate: new DateTime(2026, 7, 24, 0, 0, 0),
            ToDate: new DateTime(2026, 7, 24, 0, 0, 0));

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(50000m, result.TotalIncome);
        var point = Assert.Single(result.Series);
        Assert.Equal(50000m, point.Income);
    }
}
