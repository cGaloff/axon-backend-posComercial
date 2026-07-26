using System.Reflection;
using Axon.Application.CashRegister.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities.CashRegister;
using CashRegisterEntity = Axon.Domain.Entities.CashRegister.CashRegister;

namespace Axon.Application.Tests.CashRegister;

public class GetCashSessionsHistoryQueryHandlerTests
{
    private static void SetOpenedAt(CashSession session, DateTime openedAtUtc)
    {
        typeof(CashSession).GetProperty(nameof(CashSession.OpenedAt), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(session, openedAtUtc);
    }

    // Bug reportado por frontend: un filtro de un solo día (from == to, ambos a
    // medianoche, como envía un selector de fecha simple) no mostraba ninguna
    // sesión, porque el rango resultante medianoche-a-medianoche cubre 0
    // segundos. Además, FromDate/ToDate representan el día calendario en hora
    // Colombia, no UTC (el código anterior solo forzaba Kind=Utc sin convertir
    // el reloj, ver ColombiaTime.ToUtc).
    [Fact]
    public async Task Handle_WithSingleDayFilter_IncludesSessionsOpenedThatDayInColombiaTime()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var register = CashRegisterEntity.Create("Caja Principal", "", isDefault: false);
        var session = CashSession.Create(register.Id, userId, 50000m);

        // 24/07/2026 10:00 hora Colombia (UTC-5) == 24/07/2026 15:00 UTC.
        SetOpenedAt(session, new DateTime(2026, 7, 24, 15, 0, 0, DateTimeKind.Utc));

        dbContext.CashRegisters.Add(register);
        dbContext.CashSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var handler = new GetCashSessionsHistoryQueryHandler(dbContext);

        // El caller filtra "el día 24/07/2026" enviando la misma fecha en from y
        // to, como hace un selector de un solo día.
        var query = new GetCashSessionsHistoryQuery(
            FromDate: new DateTime(2026, 7, 24, 0, 0, 0),
            ToDate: new DateTime(2026, 7, 24, 0, 0, 0));

        var result = await handler.Handle(query, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(session.Id, item.SessionId);
    }

    // Caso simétrico al de ventas: una sesión abierta tarde en la noche en
    // Colombia (que ya cae en el día UTC siguiente) debe seguir apareciendo en
    // el reporte del día en que realmente se abrió.
    [Fact]
    public async Task Handle_IncludesSessionOpenedLateAtNightInColombiaButAlreadyNextDayInUtc()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        var register = CashRegisterEntity.Create("Caja Principal", "", isDefault: false);
        var session = CashSession.Create(register.Id, userId, 50000m);

        // 24/07/2026 23:30 hora Colombia (UTC-5) == 25/07/2026 04:30 UTC.
        SetOpenedAt(session, new DateTime(2026, 7, 25, 4, 30, 0, DateTimeKind.Utc));

        dbContext.CashRegisters.Add(register);
        dbContext.CashSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var handler = new GetCashSessionsHistoryQueryHandler(dbContext);

        var query = new GetCashSessionsHistoryQuery(
            FromDate: new DateTime(2026, 7, 24, 0, 0, 0),
            ToDate: new DateTime(2026, 7, 24, 0, 0, 0));

        var result = await handler.Handle(query, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(session.Id, item.SessionId);
    }
}
