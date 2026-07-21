using Axon.API.Common;
using Axon.API.DTOs.CashRegister;
using Axon.API.Filters;
using Axon.Application.CashRegister.Commands;
using Axon.Application.CashRegister.Queries;
using Axon.Application.Common.Models;
using Axon.Domain.Entities.CashRegister;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/cash-register")]
[Authorize]
public class CashRegisterController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashRegisterController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission("cash_register:read")]
    public async Task<IActionResult> GetCashRegisters()
    {
        var result = await _mediator.Send(new GetCashRegistersQuery());

        return Ok(ApiResponse<List<CashRegisterDto>>.Ok(result));
    }

    [HttpGet("sessions")]
    [RequirePermission("cash_register:read", "reports:read")]
    public async Task<IActionResult> GetSessionsHistory(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] CashSessionStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetCashSessionsHistoryQuery(fromDate, toDate, cashRegisterId, status, page, pageSize);

        var result = await _mediator.Send(query);

        return Ok(ApiResponse<PagedResult<CashSessionSummaryDto>>.Ok(result));
    }

    [HttpPost("sessions/open")]
    [RequirePermission("cash_register:write")]
    public async Task<IActionResult> OpenSession(OpenCashSessionRequest request)
    {
        var command = new OpenCashSessionCommand(request.CashRegisterId, request.InitialAmount);

        var result = await _mediator.Send(command);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<OpenCashSessionResult>.Ok(result, "Sesión de caja abierta"));
    }

    [HttpPost("sessions/{id:guid}/close")]
    [RequirePermission("cash_register:write")]
    public async Task<IActionResult> CloseSession(Guid id, CloseCashSessionRequest request)
    {
        var command = new CloseCashSessionCommand(id, request.CountedAmount, request.Notes, request.ForceClose);

        var result = await _mediator.Send(command);

        return Ok(ApiResponse<CloseCashSessionResult>.Ok(result, "Sesión de caja cerrada"));
    }

    [HttpGet("sessions/{id:guid}/summary")]
    [RequirePermission("cash_register:read")]
    public async Task<IActionResult> GetSessionSummary(Guid id)
    {
        var result = await _mediator.Send(new GetCashSessionSummaryQuery(id));

        return Ok(ApiResponse<CashSessionSummaryDto>.Ok(result));
    }

    [HttpPost("sessions/{id:guid}/movements")]
    [RequirePermission("cash_register:write")]
    public async Task<IActionResult> AddMovement(Guid id, AddCashMovementRequest request)
    {
        var command = new AddCashMovementCommand(id, request.Type, request.Amount, request.Description);

        var movementId = await _mediator.Send(command);

        return Ok(ApiResponse<Guid>.Ok(movementId, "Movimiento registrado"));
    }
}
