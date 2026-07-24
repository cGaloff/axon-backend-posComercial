using Axon.API.Common;
using Axon.API.DTOs.Sales;
using Axon.API.Filters;
using Axon.Application.Common.Models;
using Axon.Application.Invoicing.Queries;
using Axon.Application.Sales.Commands;
using Axon.Application.Sales.DTOs;
using Axon.Application.Sales.Queries;
using Axon.Application.TenantConfig.Queries;
using Axon.Domain.Entities.Sales;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [RequirePermission("sales:write")]
    public async Task<IActionResult> ProcessSale(ProcessSaleRequest request)
    {
        var command = new ProcessSaleCommand(
            request.Items.Select(i => new SaleItemRequest(i.ProductId, i.Quantity, i.Discount)).ToList(),
            request.Payments.Select(p => new SalePaymentRequest(p.Method, p.Amount, p.AmountTendered)).ToList(),
            request.CashRegisterId,
            request.CustomerId,
            request.CustomerName,
            request.CustomerEmail,
            request.Notes);

        var result = await _mediator.Send(command);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<ProcessSaleResult>.Ok(result, "Venta procesada exitosamente"));
    }

    [HttpPost("{id:guid}/return")]
    [RequirePermission("sales:write")]
    public async Task<IActionResult> ReturnSale(Guid id, ReturnSaleRequest request)
    {
        await _mediator.Send(new ReturnSaleCommand(id, request.Reason));

        return Ok(ApiResponse<string>.Ok("ok", "Venta devuelta exitosamente"));
    }

    [HttpGet]
    [RequirePermission("sales:read")]
    public async Task<IActionResult> GetSalesHistory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] SaleStatus? status,
        [FromQuery] Guid? customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetSalesHistoryQuery(from, to, status, customerId, page, pageSize);

        var result = await _mediator.Send(query);

        return Ok(ApiResponse<PagedResult<SaleDto>>.Ok(result));
    }

    [HttpGet("{id:guid}/receipt")]
    [RequirePermission("sales:read")]
    public async Task<IActionResult> GetReceipt(Guid id)
    {
        var pdf = await _mediator.Send(new GetSaleReceiptQuery(id));

        return File(pdf, "application/pdf", $"{id}.pdf");
    }

    [HttpGet("{id:guid}/invoice")]
    [RequirePermission("sales:read")]
    public async Task<IActionResult> GetInvoicePdf(Guid id)
    {
        var pdf = await _mediator.Send(new GetInvoicePdfBySaleIdQuery(id));

        return File(pdf, "application/pdf", $"factura-{id}.pdf");
    }

    [HttpGet("{id:guid}/tax-summary")]
    [RequirePermission("sales:read")]
    public async Task<IActionResult> GetTaxSummary(Guid id)
    {
        var result = await _mediator.Send(new GetSaleTaxSummaryQuery(id));

        return Ok(ApiResponse<SaleTaxSummaryDto>.Ok(result));
    }
}
