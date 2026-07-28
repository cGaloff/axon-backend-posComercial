using Axon.Application.Common.Behaviors;
using Axon.Application.Tests.TestSupport;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Tests.Common;

public class AuditLoggingBehaviorTests
{
    private record AuditableTestRequest(Guid EntityId) : IRequest<string>, IAuditableRequest
    {
        public string AuditAction => "Test.Action";
        public Guid? AuditEntityId => EntityId;
    }

    private record NonAuditableTestRequest : IRequest<string>;

    [Fact]
    public async Task Handle_WithAuditableRequest_WritesAuditLogEntryAfterSuccess()
    {
        var dbContext = TestDbContextFactory.Create();
        var currentUser = new FakeCurrentUserContext();
        var behavior = new AuditLoggingBehavior<AuditableTestRequest, string>(
            dbContext, new FakeUnitOfWork(dbContext), currentUser);

        var entityId = Guid.NewGuid();
        var result = await behavior.Handle(
            new AuditableTestRequest(entityId), (_) => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
        var entry = Assert.Single(dbContext.AuditLogs);
        Assert.Equal(currentUser.UserId, entry.UserId);
        Assert.Equal("Test.Action", entry.Action);
        Assert.Equal(entityId, entry.EntityId);
    }

    [Fact]
    public async Task Handle_WithNonAuditableRequest_WritesNoAuditLogEntry()
    {
        var dbContext = TestDbContextFactory.Create();
        var behavior = new AuditLoggingBehavior<NonAuditableTestRequest, string>(
            dbContext, new FakeUnitOfWork(dbContext), new FakeCurrentUserContext());

        await behavior.Handle(new NonAuditableTestRequest(), (_) => Task.FromResult("ok"), CancellationToken.None);

        Assert.Empty(dbContext.AuditLogs);
    }

    [Fact]
    public async Task Handle_WhenNextThrows_PropagatesExceptionWithoutWritingAuditLogEntry()
    {
        var dbContext = TestDbContextFactory.Create();
        var behavior = new AuditLoggingBehavior<AuditableTestRequest, string>(
            dbContext, new FakeUnitOfWork(dbContext), new FakeCurrentUserContext());

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new AuditableTestRequest(Guid.NewGuid()),
            (_) => throw new InvalidOperationException("Falló el handler real"),
            CancellationToken.None));

        Assert.Empty(dbContext.AuditLogs);
    }
}
