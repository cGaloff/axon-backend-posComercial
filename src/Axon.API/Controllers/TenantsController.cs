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

    public TenantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterTenantRequest request)
    {
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
