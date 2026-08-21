namespace Axon.Application.Tenants.Services;

public interface ISubscriptionExpirationSweeper
{
    Task SweepAsync(CancellationToken cancellationToken);
}
