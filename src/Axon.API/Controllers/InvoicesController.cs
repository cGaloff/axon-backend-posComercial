using Axon.API.Common;
using Axon.API.Filters;
using Axon.Application.Common.Models;
using Axon.Application.Invoicing.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

// Nombre deliberadamente neutral: el prompt 6 reorganiza/renombra el módulo de
// "auditoría" a "facturación" — este controller no asume ese nombre todavía,
// solo expone la consulta de Invoice construida en el prompt 5.
[ApiController]
[Route("api/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvoicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission("sales:read")]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetInvoicesQuery(from, to, page, pageSize);

        var result = await _mediator.Send(query);

        return Ok(ApiResponse<PagedResult<InvoiceDto>>.Ok(result));
    }
}
