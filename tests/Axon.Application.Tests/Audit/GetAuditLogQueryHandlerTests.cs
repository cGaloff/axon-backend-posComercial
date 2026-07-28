using Axon.Application.Audit.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;

namespace Axon.Application.Tests.Audit;

public class GetAuditLogQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEntriesNewestFirstWithResolvedUserName()
    {
        var dbContext = TestDbContextFactory.Create();

        var role = Role.Create("Administrador", "", isSystem: true);
        var user = User.Create("Ana Admin", "ana@test.com", "irrelevante", role.Id);
        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);

        var older = AuditLog.Create(user.Id, "Sale.Void", Guid.NewGuid());
        var newer = AuditLog.Create(user.Id, "TenantConfig.Update", null);
        dbContext.AuditLogs.AddRange(older, newer);
        await dbContext.SaveChangesAsync();

        var handler = new GetAuditLogQueryHandler(dbContext);

        var result = await handler.Handle(new GetAuditLogQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        var items = result.Items.ToList();
        Assert.Equal("Ana Admin", items[0].UserFullName);
    }

    [Fact]
    public async Task Handle_FiltersByUserId()
    {
        var dbContext = TestDbContextFactory.Create();

        var role = Role.Create("Administrador", "", isSystem: true);
        var userA = User.Create("Ana Admin", "ana@test.com", "irrelevante", role.Id);
        var userB = User.Create("Beto Admin", "beto@test.com", "irrelevante", role.Id);
        dbContext.Roles.Add(role);
        dbContext.Users.AddRange(userA, userB);

        dbContext.AuditLogs.Add(AuditLog.Create(userA.Id, "Sale.Void", null));
        dbContext.AuditLogs.Add(AuditLog.Create(userB.Id, "Sale.Void", null));
        await dbContext.SaveChangesAsync();

        var handler = new GetAuditLogQueryHandler(dbContext);

        var result = await handler.Handle(new GetAuditLogQuery(UserId: userA.Id), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(userA.Id, item.UserId);
    }
}
