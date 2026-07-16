using Axon.API.Common;
using Axon.API.DTOs.TenantConfig;
using Axon.API.Filters;
using Axon.Application.TenantConfig.Commands;
using Axon.Application.TenantConfig.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/tenant-config")]
[Authorize]
public class TenantConfigController : ControllerBase
{
    private readonly IMediator _mediator;

    public TenantConfigController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission("configuration:read")]
    public async Task<IActionResult> GetTenantConfig()
    {
        var result = await _mediator.Send(new GetTenantConfigQuery());

        return Ok(ApiResponse<TenantConfigDto>.Ok(result));
    }

    [HttpPut]
    [RequirePermission("configuration:write")]
    public async Task<IActionResult> UpdateTenantConfig(UpdateTenantConfigRequest request)
    {
        var command = new UpdateTenantConfigCommand(
            request.BusinessName,
            request.Nit,
            request.Address,
            request.Phone,
            request.Email,
            request.Website,
            request.LogoUrl,
            request.IsResponsableIva);

        await _mediator.Send(command);

        return Ok(ApiResponse<string>.Ok("ok", "Configuración actualizada exitosamente"));
    }
}
