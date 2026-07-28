using Axon.Application.Audit.Queries;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;

namespace Axon.Application.Tests.Audit;

public class GetLoginHistoryQueryHandlerTests
{
    [Fact]
    public async Task Handle_FiltersBySuccessOnly()
    {
        var dbContext = TestDbContextFactory.Create();

        dbContext.LoginAttempts.Add(LoginAttempt.Create("ana@test.com", Guid.NewGuid(), success: true, "127.0.0.1"));
        dbContext.LoginAttempts.Add(LoginAttempt.Create("ana@test.com", Guid.NewGuid(), success: false, "127.0.0.1"));
        await dbContext.SaveChangesAsync();

        var handler = new GetLoginHistoryQueryHandler(dbContext);

        var failedOnly = await handler.Handle(new GetLoginHistoryQuery(SuccessOnly: false), CancellationToken.None);

        var item = Assert.Single(failedOnly.Items);
        Assert.False(item.Success);
    }
}
