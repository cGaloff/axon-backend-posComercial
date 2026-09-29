using Axon.Application.Tenants.Commands;

namespace Axon.Application.Tests.Tenants;

public class TenantSubscriptionValidatorTests
{
    private static RegisterTenantCommand ComandoDeRegistro(DateTime? subscriptionEndsAt) =>
        new("Panadería", "panaderia", "duena@panaderia.co", "clave-segura", "basic", subscriptionEndsAt);

    [Fact]
    public void Registro_SinFechaDeVencimiento_EsValido()
    {
        var resultado = new RegisterTenantCommandValidator().Validate(ComandoDeRegistro(null));

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public void Registro_ConFechaFutura_EsValido()
    {
        var resultado = new RegisterTenantCommandValidator()
            .Validate(ComandoDeRegistro(DateTime.UtcNow.AddDays(5)));

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public void Registro_ConFechaPasada_EsRechazado()
    {
        var resultado = new RegisterTenantCommandValidator()
            .Validate(ComandoDeRegistro(DateTime.UtcNow.AddDays(-1)));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.ErrorMessage == "La fecha de vencimiento debe ser futura");
    }

    [Fact]
    public void Registro_ConPlanTrial_EsRechazado()
    {
        // El trial no es un plan: se aprovisiona como "basic" con fecha de corte.
        var comando = new RegisterTenantCommand(
            "Panadería", "panaderia", "duena@panaderia.co", "clave-segura", "trial", DateTime.UtcNow.AddDays(5));

        var resultado = new RegisterTenantCommandValidator().Validate(comando);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.ErrorMessage == "Plan inválido");
    }

    [Fact]
    public void ActualizarSuscripcion_ConFechaNull_EsValido()
    {
        var resultado = new UpdateTenantSubscriptionCommandValidator()
            .Validate(new UpdateTenantSubscriptionCommand("panaderia", null));

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public void ActualizarSuscripcion_ConFechaPasada_EsValido()
    {
        // Cortar el acceso de inmediato ante un impago es un uso legítimo.
        var resultado = new UpdateTenantSubscriptionCommandValidator()
            .Validate(new UpdateTenantSubscriptionCommand("panaderia", DateTime.UtcNow.AddDays(-1)));

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public void ActualizarSuscripcion_ConPlanInvalido_EsRechazado()
    {
        var resultado = new UpdateTenantSubscriptionCommandValidator()
            .Validate(new UpdateTenantSubscriptionCommand("panaderia", DateTime.UtcNow.AddDays(30), "gratis"));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, e => e.ErrorMessage == "Plan inválido");
    }

    [Fact]
    public void ActualizarSuscripcion_SinSlug_EsRechazado()
    {
        var resultado = new UpdateTenantSubscriptionCommandValidator()
            .Validate(new UpdateTenantSubscriptionCommand(string.Empty, DateTime.UtcNow.AddDays(30)));

        Assert.False(resultado.IsValid);
    }
}
