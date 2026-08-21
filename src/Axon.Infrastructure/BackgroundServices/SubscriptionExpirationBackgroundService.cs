using Axon.Application.Tenants.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Axon.Infrastructure.BackgroundServices;

// Barre periódicamente public.tenants para bloquear los que vencieron y
// mandar los recordatorios de los que están por vencer. Toda la lógica de
// negocio vive en ISubscriptionExpirationSweeper (testeable sin timer); esta
// clase solo es la infraestructura del reloj.
public class SubscriptionExpirationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SubscriptionExpirationBackgroundService> _logger;

    public SubscriptionExpirationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SubscriptionExpirationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = int.TryParse(_configuration["Subscription:CheckIntervalMinutes"], out var minutes)
            ? minutes
            : 60;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sweeper = scope.ServiceProvider.GetRequiredService<ISubscriptionExpirationSweeper>();
                await sweeper.SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falló el barrido de vencimiento de suscripciones.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
