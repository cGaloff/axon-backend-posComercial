using Axon.Application.Tenants.Services;
using Axon.Application.Tests.TestSupport;
using Axon.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Tests.Tenants;

public class SubscriptionExpirationSweeperTests
{
    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Subscription:ReminderDaysBeforeExpiry"] = "5"
            })
            .Build();

    private static (SubscriptionExpirationSweeper Sweeper, Axon.Infrastructure.Persistence.AppDbContext DbContext, FakeEmailService EmailService) Arrange()
    {
        var dbContext = TestMasterDbContextFactory.Create();
        var emailService = new FakeEmailService();
        var sweeper = new SubscriptionExpirationSweeper(dbContext, emailService, BuildConfiguration());

        return (sweeper, dbContext, emailService);
    }

    [Fact]
    public async Task SweepAsync_WithExpiredTenant_SuspendsAndSendsExpiredEmail()
    {
        var (sweeper, dbContext, emailService) = Arrange();
        var tenant = Tenant.Create("expirado", "Expirado SA", "basic", "owner@test.com", DateTime.UtcNow.AddDays(-1));
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        await sweeper.SweepAsync(CancellationToken.None);

        var updated = dbContext.Tenants.Single(t => t.Id == tenant.Id);
        Assert.False(updated.IsActive);
        Assert.Single(emailService.TrialExpiredEmails);
        Assert.Equal("owner@test.com", emailService.TrialExpiredEmails[0]);
    }

    [Fact]
    public async Task SweepAsync_WithinReminderWindow_SendsReminderAndMarksSent()
    {
        var (sweeper, dbContext, emailService) = Arrange();
        var tenant = Tenant.Create("por-vencer", "Por Vencer SA", "basic", "owner@test.com", DateTime.UtcNow.AddDays(3));
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        await sweeper.SweepAsync(CancellationToken.None);

        var updated = dbContext.Tenants.Single(t => t.Id == tenant.Id);
        Assert.True(updated.IsActive);
        Assert.NotNull(updated.LastTrialReminderSentAt);
        var reminder = Assert.Single(emailService.TrialReminders);
        Assert.Equal("owner@test.com", reminder.Email);
        Assert.Equal(3, reminder.DaysRemaining);
    }

    [Fact]
    public async Task SweepAsync_CalledTwiceSameDay_DoesNotResendReminder()
    {
        var (sweeper, dbContext, emailService) = Arrange();
        var tenant = Tenant.Create("por-vencer", "Por Vencer SA", "basic", "owner@test.com", DateTime.UtcNow.AddDays(3));
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        await sweeper.SweepAsync(CancellationToken.None);
        await sweeper.SweepAsync(CancellationToken.None);

        Assert.Single(emailService.TrialReminders);
    }

    [Fact]
    public async Task SweepAsync_WithOutsideReminderWindow_DoesNothing()
    {
        var (sweeper, dbContext, emailService) = Arrange();
        var tenant = Tenant.Create("recien-creado", "Recien Creado SA", "basic", "owner@test.com", DateTime.UtcNow.AddDays(6));
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        await sweeper.SweepAsync(CancellationToken.None);

        var updated = dbContext.Tenants.Single(t => t.Id == tenant.Id);
        Assert.True(updated.IsActive);
        Assert.Null(updated.LastTrialReminderSentAt);
        Assert.Empty(emailService.TrialReminders);
        Assert.Empty(emailService.TrialExpiredEmails);
    }

    [Fact]
    public async Task SweepAsync_WithGrandfatheredTenant_IsNeverTouched()
    {
        var (sweeper, dbContext, emailService) = Arrange();
        // Tenant sin SubscriptionExpiresAt (ej. uno de los ya existentes antes
        // de este sistema) — Tenant.Create siempre lo setea, así que se
        // simula el estado "grandfathered" agregando directo a la base
        // InMemory sin pasar por el constructor de dominio.
        var tenant = Tenant.Create("viejo", "Tenant Viejo", "basic", "owner@test.com", DateTime.UtcNow.AddDays(-100));
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        dbContext.Entry(tenant).Property("SubscriptionExpiresAt").CurrentValue = null;
        await dbContext.SaveChangesAsync();

        await sweeper.SweepAsync(CancellationToken.None);

        var updated = dbContext.Tenants.Single(t => t.Id == tenant.Id);
        Assert.True(updated.IsActive);
        Assert.Empty(emailService.TrialExpiredEmails);
    }
}
