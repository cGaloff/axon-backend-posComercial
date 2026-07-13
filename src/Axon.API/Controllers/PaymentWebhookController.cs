using Axon.API.DTOs.Sales;
using Axon.Application.Sales.Commands;
using Axon.Infrastructure.MultiTenant;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/webhooks")]
public class PaymentWebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly TenantResolver _tenantResolver;
    private readonly TenantContext _tenantContext;
    private readonly IConfiguration _configuration;

    public PaymentWebhookController(
        IMediator mediator,
        TenantResolver tenantResolver,
        TenantContext tenantContext,
        IConfiguration configuration)
    {
        _mediator = mediator;
        _tenantResolver = tenantResolver;
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    [HttpPost("payment-confirmed")]
    [AllowAnonymous]
    public async Task<IActionResult> PaymentConfirmed(PaymentConfirmedWebhookRequest request)
    {
        var expectedSecret = _configuration["Webhooks:PaymentSecret"];

        if (string.IsNullOrEmpty(expectedSecret) ||
            !Request.Headers.TryGetValue("X-Webhook-Secret", out var providedSecret) ||
            providedSecret != expectedSecret)
        {
            return Unauthorized();
        }

        var tenant = await _tenantResolver.ResolveAsync(request.TenantSlug);

        if (tenant is null || !tenant.IsActive)
        {
            return NotFound();
        }

        _tenantContext.SetTenant(tenant.Slug, tenant.SchemaName);

        await _mediator.Send(new ConfirmSalePaymentCommand(request.SaleId));

        return Ok();
    }
}
