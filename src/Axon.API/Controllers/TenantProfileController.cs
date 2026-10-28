using Axon.API.Common;
using Axon.API.DTOs.TenantConfig;
using Axon.API.Filters;
using Axon.Application.TenantConfig.Commands;
using Axon.Application.TenantConfig.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

// Perfil comercial del establecimiento para el portal web. Comparte datos con
// /api/tenant-config (mismo registro tenant_config), con los permisos de
// configuración: leer lo tiene Propietario/Administrador/Auditor, editar solo
// el Propietario.
[ApiController]
[Route("api/tenants/profile")]
[Authorize]
public class TenantProfileController : ControllerBase
{
    private readonly IMediator _mediator;

    public TenantProfileController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission("configuration:read")]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _mediator.Send(new GetTenantProfileQuery());

        return Ok(ApiResponse<TenantProfileDto>.Ok(result));
    }

    [HttpPut]
    [RequirePermission("configuration:write")]
    public async Task<IActionResult> UpdateProfile(UpdateTenantProfileRequest request)
    {
        var command = new UpdateTenantProfileCommand(
            request.BusinessName,
            request.LegalName,
            request.Nit,
            request.Phone,
            request.Address,
            request.City,
            request.State,
            request.TaxRegime);

        var result = await _mediator.Send(command);

        return Ok(ApiResponse<UpdateTenantProfileResult>.Ok(result, "Información del establecimiento actualizada exitosamente."));
    }
}
