using Axon.API.Common;
using Axon.API.Filters;
using Axon.Application.Audit.Queries;
using Axon.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

// audit:read: solo Propietario y Auditor (Matriz de Roles y Permisos v2) — el
// Administrador no tiene acceso al log de auditoría según el documento.
[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("log")]
    [RequirePermission("audit:read")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetAuditLogQuery(from, to, userId, page, pageSize));

        return Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(result));
    }

    [HttpGet("login-history")]
    [RequirePermission("audit:read")]
    public async Task<IActionResult> GetLoginHistory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool? successOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetLoginHistoryQuery(from, to, successOnly, page, pageSize));

        return Ok(ApiResponse<PagedResult<LoginAttemptDto>>.Ok(result));
    }
}
