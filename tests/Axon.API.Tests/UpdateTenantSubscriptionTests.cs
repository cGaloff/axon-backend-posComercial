using Axon.API.Controllers;
using Axon.API.DTOs.Tenants;
using Axon.Application.Interfaces;
using Axon.Application.Tenants.Commands;
using Axon.Domain.Entities;
using Axon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Axon.API.Tests;

public class UpdateTenantSubscriptionTests
{
    [Fact]
    public async Task Handler_ActualizaLaFechaDeVencimiento()
    {
        await using var dbContext = CrearDbContext();
        dbContext.Tenants.Add(Tenant.Create("panaderia", "Panadería", "basic", subscriptionExpiresAt: DateTime.UtcNow.AddDays(-1)));
        await dbContext.SaveChangesAsync();

        var nuevaFecha = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc);
        var invalidador = new InvalidadorDeCacheEspia();

        var resultado = await CrearHandler(dbContext, invalidador).Handle(
            new UpdateTenantSubscriptionCommand("panaderia", nuevaFecha, "basic"),
            CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(nuevaFecha, resultado!.SubscriptionEndsAt);
        Assert.Equal("basic", resultado.Plan);

        var persistido = await dbContext.Tenants.SingleAsync(t => t.Slug == "panaderia");
        Assert.Equal(nuevaFecha, persistido.SubscriptionExpiresAt);
        Assert.False(persistido.IsSubscriptionExpired(DateTime.UtcNow));
        Assert.Contains("panaderia", invalidador.SlugsInvalidados);
    }

    [Fact]
    public async Task Handler_ConFechaNull_DejaAlTenantSinVencimiento()
    {
        await using var dbContext = CrearDbContext();
        dbContext.Tenants.Add(Tenant.Create("panaderia", "Panadería", "basic", subscriptionExpiresAt: DateTime.UtcNow.AddDays(5)));
        await dbContext.SaveChangesAsync();

        var resultado = await CrearHandler(dbContext).Handle(
            new UpdateTenantSubscriptionCommand("panaderia", null),
            CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Null(resultado!.SubscriptionEndsAt);
        Assert.Null((await dbContext.Tenants.SingleAsync(t => t.Slug == "panaderia")).SubscriptionExpiresAt);
    }

    [Fact]
    public async Task Handler_ConSlugInexistente_DevuelveNull()
    {
        await using var dbContext = CrearDbContext();

        var resultado = await CrearHandler(dbContext).Handle(
            new UpdateTenantSubscriptionCommand("no-existe", DateTime.UtcNow.AddDays(30)),
            CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Endpoint_SinElSecretoDeAprovisionamiento_Devuelve401()
    {
        // Sin secreto no se llega al mediador, por eso puede ir nulo.
        var controller = CrearController(secretoEnviado: null);

        var resultado = await controller.UpdateSubscription(
            "panaderia",
            new UpdateTenantSubscriptionRequest(DateTime.UtcNow.AddDays(30)));

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    [Fact]
    public async Task Endpoint_ConElSecretoEquivocado_Devuelve401()
    {
        var controller = CrearController(secretoEnviado: "secreto-incorrecto");

        var resultado = await controller.UpdateSubscription(
            "panaderia",
            new UpdateTenantSubscriptionRequest(DateTime.UtcNow.AddDays(30)));

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    private static UpdateTenantSubscriptionCommandHandler CrearHandler(
        AppDbContext dbContext,
        ITenantCacheInvalidator? invalidador = null)
    {
        return new UpdateTenantSubscriptionCommandHandler(
            dbContext,
            invalidador ?? new InvalidadorDeCacheEspia(),
            NullLogger<UpdateTenantSubscriptionCommandHandler>.Instance);
    }

    private static TenantsController CrearController(string? secretoEnviado)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Provisioning:Secret"] = "secreto-real" })
            .Build();

        var controller = new TenantsController(null!, configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        if (secretoEnviado is not null)
        {
            controller.ControllerContext.HttpContext.Request.Headers["X-Provisioning-Secret"] = secretoEnviado;
        }

        return controller;
    }

    private static AppDbContext CrearDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tenants_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class InvalidadorDeCacheEspia : ITenantCacheInvalidator
    {
        public List<string> SlugsInvalidados { get; } = new();

        public void Invalidate(string slug) => SlugsInvalidados.Add(slug);
    }
}
