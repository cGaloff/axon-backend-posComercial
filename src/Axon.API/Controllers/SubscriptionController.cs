using Axon.API.Common;
using Axon.API.DTOs.Subscription;
using Axon.Application.Tenants.Commands;
using Axon.Application.Tenants.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/subscription")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public SubscriptionController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    // Auto-servicio (sin RequirePermission adicional, solo estar autenticado):
    // cualquier usuario del tenant puede ver cuánto le queda de suscripción.
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var result = await _mediator.Send(new GetSubscriptionStatusQuery());

        return Ok(ApiResponse<SubscriptionStatusResult>.Ok(result, "ok"));
    }

    // Público (landing y pantalla de bloqueo): así el frontend anuncia los
    // mismos días de prueba que se asignan al registrarse.
    [HttpGet("trial-info")]
    [AllowAnonymous]
    public IActionResult GetTrialInfo()
    {
        var trialDays = int.TryParse(_configuration["Subscription:TrialDays"], out var days) ? days : 5;

        return Ok(ApiResponse<TrialInfoResponse>.Ok(new TrialInfoResponse(trialDays), "ok"));
    }

    // Punto de entrada manual mientras no hay pasarela de pagos integrada —
    // protegido por secreto (no JWT), mismo patrón que
    // PaymentWebhookController. El día que se integre una pasarela real, su
    // webhook llama acá en vez de hacerlo un humano a mano.
    [HttpPost("extend")]
    [AllowAnonymous]
    public async Task<IActionResult> Extend(ExtendSubscriptionRequest request)
    {
        var expectedSecret = _configuration["Subscription:ExtensionSecret"];

        if (string.IsNullOrEmpty(expectedSecret) ||
            !Request.Headers.TryGetValue("X-Subscription-Secret", out var providedSecret) ||
            providedSecret != expectedSecret)
        {
            return Unauthorized();
        }

        var command = new ExtendTenantSubscriptionCommand(
            request.Slug,
            request.ExtensionDays,
            request.BillingCycle,
            request.Amount,
            request.TransactionReference);

        var result = await _mediator.Send(command);

        // 200 también en un reintento: la pasarela debe dejar de reintentar.
        var message = result.AlreadyProcessed
            ? "La transacción ya había sido aplicada"
            : "Suscripción extendida";

        return Ok(ApiResponse<ExtendTenantSubscriptionResult>.Ok(result, message));
    }
}
