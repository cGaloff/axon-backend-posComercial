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
        // Este endpoint da de alta empresas y estaba abierto: cualquiera con la
        // URL podía crearse una sin pagar. Solo lo llama el backend de
        // facturación, que se identifica con este secreto compartido.
        var expectedSecret = _configuration["Provisioning:Secret"];

        if (string.IsNullOrEmpty(expectedSecret) ||
            !Request.Headers.TryGetValue("X-Provisioning-Secret", out var providedSecret) ||
            providedSecret != expectedSecret)
        {
            return Unauthorized();
        }

        var command = new RegisterTenantCommand(
            request.BusinessName,
            request.Slug,
            request.OwnerEmail,
            request.OwnerPassword,
            request.Plan);

        var result = await _mediator.Send(command);

        return Ok(ApiResponse<RegisterTenantResult>.Ok(result, "Tenant registrado exitosamente"));
    }
}
