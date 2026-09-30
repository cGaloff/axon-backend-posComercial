using Axon.Application.Tenants.Commands;
using Axon.Application.Tests.TestSupport;

namespace Axon.Application.Tests.Tenants;

public class ProvisionTenantCommandHandlerTests
{
    [Fact]
    public async Task Handle_SinFecha_CreaSinPruebaNiVencimientoYHasheaLaClave()
    {
        var mediator = new FakeRegisterTenantMediator();
        var hasher = new FakePasswordHasher();
        var handler = new ProvisionTenantCommandHandler(mediator, hasher);

        await handler.Handle(
            new ProvisionTenantCommand("Panadería", "panaderia", "duena@panaderia.co", "clave-segura", "basic"),
            CancellationToken.None);

        var enviado = mediator.LastCommand!;
        Assert.False(enviado.StartTrial);
        Assert.Null(enviado.SubscriptionExpiresAt);
        Assert.Equal(hasher.Hash("clave-segura"), enviado.OwnerPasswordHash);
    }

    [Fact]
    public async Task Handle_ConFecha_LaPasaTalCual()
    {
        var mediator = new FakeRegisterTenantMediator();
        var handler = new ProvisionTenantCommandHandler(mediator, new FakePasswordHasher());
        var vence = DateTime.UtcNow.AddDays(7);

        await handler.Handle(
            new ProvisionTenantCommand("Panadería", "panaderia", "duena@panaderia.co", "clave-segura", "basic", vence),
            CancellationToken.None);

        Assert.False(mediator.LastCommand!.StartTrial);
        Assert.Equal(vence, mediator.LastCommand.SubscriptionExpiresAt);
    }
}
