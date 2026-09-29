using System.Text.Json;
using Axon.API.Middleware;
using Axon.Domain.Entities;
using Axon.Infrastructure.MultiTenant;
using Axon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Axon.API.Tests;

public class TenantResolutionMiddlewareTests
{
    [Fact]
    public async Task Tenant_ConSuscripcionVencida_Recibe403()
    {
        var tenant = Tenant.Create("panaderia", "Panadería", "basic", DateTime.UtcNow.AddDays(-1));

        var (context, siguienteEjecutado) = await EjecutarMiddlewareAsync(tenant);

        Assert.False(siguienteEjecutado());
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("Suscripción vencida.", await LeerMensajeAsync(context));
    }

    [Fact]
    public async Task Tenant_SinVencimiento_PasaAlSiguienteMiddleware()
    {
        var tenant = Tenant.Create("panaderia", "Panadería", "basic");

        var (context, siguienteEjecutado) = await EjecutarMiddlewareAsync(tenant);

        Assert.True(siguienteEjecutado());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Tenant_ConVencimientoFuturo_PasaAlSiguienteMiddleware()
    {
        var tenant = Tenant.Create("panaderia", "Panadería", "basic", DateTime.UtcNow.AddDays(5));

        var (_, siguienteEjecutado) = await EjecutarMiddlewareAsync(tenant);

        Assert.True(siguienteEjecutado());
    }

    [Fact]
    public async Task Tenant_SuspendidoYVencido_RecibeElMensajeDeSuspension()
    {
        // IsActive manda sobre la fecha: son dos motivos distintos de bloqueo.
        var tenant = Tenant.Create("panaderia", "Panadería", "basic", DateTime.UtcNow.AddDays(-1));
        Suspender(tenant);

        var (context, _) = await EjecutarMiddlewareAsync(tenant);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("Tenant suspendido.", await LeerMensajeAsync(context));
    }

    [Fact]
    public async Task PatchDeSuscripcion_NoExigeElHeaderDeTenant()
    {
        // El endpoint de renovación tiene que funcionar con el tenant ya vencido.
        var (context, siguienteEjecutado) = await EjecutarMiddlewareAsync(
            Tenant.Create("panaderia", "Panadería", "basic", DateTime.UtcNow.AddDays(-1)),
            path: "/api/tenants/panaderia/subscription",
            enviarHeaderDeTenant: false);

        Assert.True(siguienteEjecutado());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static async Task<(HttpContext Context, Func<bool> SiguienteEjecutado)> EjecutarMiddlewareAsync(
        Tenant tenant,
        string path = "/api/products",
        bool enviarHeaderDeTenant = true)
    {
        await using var dbContext = CrearDbContext();
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var resolver = new TenantResolver(dbContext, new MemoryCache(new MemoryCacheOptions()));

        var seLlamoAlSiguiente = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            seLlamoAlSiguiente = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (enviarHeaderDeTenant)
        {
            context.Request.Headers["X-Tenant-Slug"] = tenant.Slug;
        }

        await middleware.InvokeAsync(context, resolver, new TenantContext());

        return (context, () => seLlamoAlSiguiente);
    }

    private static AppDbContext CrearDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tenants_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<string?> LeerMensajeAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        using var documento = await JsonDocument.ParseAsync(context.Response.Body);

        return documento.RootElement.GetProperty("message").GetString();
    }

    // IsActive no tiene setter público y no se toca en esta funcionalidad; para
    // el caso de prueba se fija por reflexión.
    private static void Suspender(Tenant tenant)
    {
        typeof(Tenant).GetProperty(nameof(Tenant.IsActive))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(tenant, new object[] { false });
    }
}
