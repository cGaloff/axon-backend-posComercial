using Axon.API.Controllers;
using Axon.API.DTOs.Tenants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Axon.API.Tests;

public class TenantRegistrationEndpointTests
{
    private static readonly RegisterTenantRequest Solicitud =
        new("Panadería", "panaderia", "duena@panaderia.co", "clave-segura", "basic");

    [Fact]
    public async Task Register_SinSecretoYAutoRegistroApagado_Devuelve401()
    {
        // Es el comportamiento de prod: las altas solo entran por el backend de pagos.
        var controller = CrearController(autoRegistro: null, secretoEnviado: null);

        var resultado = await controller.Register(Solicitud);

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    [Fact]
    public async Task Register_ConSecretoEquivocado_Devuelve401AunqueElAutoRegistroEsteEncendido()
    {
        var controller = CrearController(autoRegistro: "true", secretoEnviado: "secreto-incorrecto");

        var resultado = await controller.Register(Solicitud);

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    [Fact]
    public async Task ConfirmRegistration_ConAutoRegistroApagado_Devuelve401()
    {
        var controller = CrearController(autoRegistro: "false", secretoEnviado: null);

        var resultado = await controller.ConfirmRegistration(
            new ConfirmTenantRegistrationRequest(Guid.NewGuid(), "123456"));

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    private static TenantsController CrearController(string? autoRegistro, string? secretoEnviado)
    {
        var valores = new Dictionary<string, string?> { ["Provisioning:Secret"] = "secreto-real" };
        if (autoRegistro is not null)
        {
            valores["Registration:SelfServiceEnabled"] = autoRegistro;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        // En estos casos no se llega al mediador, por eso puede ir nulo.
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
}
