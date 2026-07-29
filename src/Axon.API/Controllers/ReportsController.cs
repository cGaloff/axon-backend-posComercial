using Axon.API.Common;
using Axon.API.Filters;
using Axon.Application.Reports.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("cash-flow")]
    [RequirePermission("reports:read")]
    public async Task<IActionResult> GetCashFlow(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] string groupBy = "Day")
    {
        var result = await _mediator.Send(new GetCashFlowReportQuery(fromDate, toDate, groupBy));

        return Ok(ApiResponse<CashFlowReportDto>.Ok(result));
    }

    [HttpGet("sales-summary")]
    [RequirePermission("reports:read", "sales:read")]
    public async Task<IActionResult> GetSalesSummary([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _mediator.Send(new GetSalesSummaryReportQuery(fromDate, toDate));

        return Ok(ApiResponse<SalesSummaryReportDto>.Ok(result));
    }

    [HttpGet("inventory-summary")]
    [RequirePermission("reports:read", "inventory:read")]
    public async Task<IActionResult> GetInventorySummary()
    {
        var result = await _mediator.Send(new GetInventorySummaryReportQuery());

        return Ok(ApiResponse<InventorySummaryReportDto>.Ok(result));
    }

    [HttpGet("sales-by-employee")]
    [RequirePermission("reports:read", "sales:read")]
    public async Task<IActionResult> GetSalesByEmployee([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _mediator.Send(new GetSalesByEmployeeReportQuery(fromDate, toDate));

        return Ok(ApiResponse<SalesByEmployeeReportDto>.Ok(result));
    }

    // Mismo permiso que inventory-summary (no sales-summary): el costo de
    // producto es información más sensible que el precio de venta, y hasta
    // ahora solo se exponía a quien tiene acceso a inventario.
    [HttpGet("profit")]
    [RequirePermission("reports:read", "inventory:read")]
    public async Task<IActionResult> GetProfit([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var result = await _mediator.Send(new GetProfitReportQuery(fromDate, toDate));

        return Ok(ApiResponse<ProfitReportDto>.Ok(result));
    }
}
