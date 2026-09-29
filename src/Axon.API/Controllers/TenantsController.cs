using Axon.API.Common;
using Axon.API.DTOs.Tenants;
using Axon.Application.Tenants.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private const string ProvisioningSecretHeader = "X-Provisioning-Secret";

    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public TenantsController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterTenantRequest request)
    {
        // Dos caminos en la misma ruta, que es la que ya usa el backend de pagos:
        // - Con X-Provisioning-Secret: alta inmediata desde pagos, con la fecha
        //   de corte que mande (null = sin vencimiento).
        // - Sin el header: auto-registro público con prueba gratis, que exige
        //   verificar el email antes de crear el tenant.
        // Un header presente pero incorrecto es 401 y no cae al auto-registro,
        // para que un secreto mal configurado en pagos no pase desapercibido.
        if (Request.Headers.ContainsKey(ProvisioningSecretHeader))
        {
            if (!HasValidProvisioningSecret())
            {
                return Unauthorized();
            }

            var provisionCommand = new ProvisionTenantCommand(
                request.BusinessName,
                request.Slug,
                request.OwnerEmail,
                request.OwnerPassword,
                request.Plan,
                request.SubscriptionEndsAt);

            var provisioned = await _mediator.Send(provisionCommand);

            return Ok(ApiResponse<RegisterTenantResult>.Ok(provisioned, "Tenant registrado exitosamente"));
        }

        var command = new RequestTenantRegistrationCommand(
            request.BusinessName,
            request.Slug,
            request.OwnerEmail,
            request.OwnerPassword,
            request.Plan);

        var result = await _mediator.Send(command);

        return Ok(ApiResponse<RequestTenantRegistrationResult>.Ok(
            result, "Te enviamos un código de verificación a tu correo. Confirmalo para activar tu cuenta."));
    }

    [HttpPost("register/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmRegistration(ConfirmTenantRegistrationRequest request)
    {
        var command = new ConfirmTenantRegistrationCommand(request.PendingRegistrationId, request.Code);

        var result = await _mediator.Send(command);

        return Ok(ApiResponse<RegisterTenantResult>.Ok(result, "Cuenta creada exitosamente"));
    }

    // El backend de pagos lo llama al convertir una prueba gratis en plan pagado y en
    // cada renovación; se protege con el mismo secreto que el registro.
    [HttpPatch("{slug}/subscription")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateSubscription(string slug, UpdateTenantSubscriptionRequest request)
    {
        if (!HasValidProvisioningSecret())
        {
            return Unauthorized();
        }

        var command = new UpdateTenantSubscriptionCommand(slug, request.SubscriptionEndsAt, request.Plan);

        var result = await _mediator.Send(command);

        if (result is null)
        {
            return NotFound(ApiResponse<object>.Fail("Tenant no encontrado"));
        }

        return Ok(ApiResponse<UpdateTenantSubscriptionResult>.Ok(result, "Suscripción actualizada exitosamente"));
    }

    private bool HasValidProvisioningSecret()
    {
        var expectedSecret = _configuration["Provisioning:Secret"];

        return !string.IsNullOrEmpty(expectedSecret) &&
               Request.Headers.TryGetValue(ProvisioningSecretHeader, out var providedSecret) &&
               providedSecret == expectedSecret;
    }
}
