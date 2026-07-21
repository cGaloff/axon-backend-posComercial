using Axon.API.Common;
using Axon.API.DTOs.Suppliers;
using Axon.API.Filters;
using Axon.Application.Common.Models;
using Axon.Application.Suppliers.Commands;
using Axon.Application.Suppliers.Queries;
using Axon.Domain.Entities.Suppliers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;

    public SuppliersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission("suppliers:read")]
    public async Task<IActionResult> GetSuppliers([FromQuery] string? search, [FromQuery] bool includeInactive = false)
    {
        var result = await _mediator.Send(new GetSuppliersQuery(search, includeInactive));

        return Ok(ApiResponse<List<SupplierDto>>.Ok(result));
    }

    [HttpPost]
    [RequirePermission("suppliers:write")]
    public async Task<IActionResult> CreateSupplier(CreateSupplierRequest request)
    {
        var command = new CreateSupplierCommand(
            request.Name,
            request.Nit,
            request.ContactName,
            request.Phone,
            request.Email,
            request.Address,
            request.City,
            request.PaymentTermDays);

        var id = await _mediator.Send(command);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.Ok(id, "Proveedor creado exitosamente"));
    }

    [HttpGet("{id:guid}/account-statement")]
    [RequirePermission("suppliers:read")]
    public async Task<IActionResult> GetAccountStatement(Guid id, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var result = await _mediator.Send(new GetSupplierAccountStatementQuery(id, from, to));

        return Ok(ApiResponse<SupplierAccountStatementDto>.Ok(result));
    }

    [HttpGet("purchase-orders")]
    [RequirePermission("suppliers:read")]
    public async Task<IActionResult> GetPurchaseOrders(
        [FromQuery] Guid? supplierId,
        [FromQuery] PurchaseOrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetPurchaseOrdersQuery(supplierId, status, page, pageSize));

        return Ok(ApiResponse<PagedResult<PurchaseOrderDto>>.Ok(result));
    }

    [HttpGet("purchase-orders/{id:guid}")]
    [RequirePermission("suppliers:read")]
    public async Task<IActionResult> GetPurchaseOrderById(Guid id)
    {
        var result = await _mediator.Send(new GetPurchaseOrderByIdQuery(id));

        return Ok(ApiResponse<PurchaseOrderDetailsDto>.Ok(result));
    }

    [HttpPost("purchase-orders")]
    [RequirePermission("suppliers:write")]
    public async Task<IActionResult> CreatePurchaseOrder(CreatePurchaseOrderRequest request)
    {
        var command = new CreatePurchaseOrderCommand(
            request.SupplierId,
            request.Items.Select(i => new PurchaseOrderItemRequest(i.ProductId, i.QuantityOrdered, i.UnitCost)).ToList(),
            request.ExpectedDate,
            request.Notes);

        var id = await _mediator.Send(command);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.Ok(id, "Orden de compra creada exitosamente"));
    }

    [HttpPost("purchase-orders/{id:guid}/receive")]
    [RequirePermission("suppliers:write")]
    public async Task<IActionResult> ReceivePurchaseOrder(Guid id, ReceivePurchaseOrderRequest request)
    {
        var command = new ReceivePurchaseOrderCommand(
            id,
            request.Items.Select(i => new ReceiptItemRequest(i.PurchaseOrderItemId, i.QuantityReceived)).ToList(),
            request.Notes);

        var result = await _mediator.Send(command);

        return Ok(ApiResponse<ReceivePurchaseOrderResult>.Ok(result, "Recepción registrada exitosamente"));
    }

    [HttpPost("{id:guid}/payments")]
    [RequirePermission("suppliers:write")]
    public async Task<IActionResult> RegisterPayment(Guid id, RegisterSupplierPaymentRequest request)
    {
        var command = new RegisterSupplierPaymentCommand(
            id,
            request.Amount,
            request.PaymentMethod,
            request.Reference,
            request.Notes);

        var paymentId = await _mediator.Send(command);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.Ok(paymentId, "Pago registrado exitosamente"));
    }
}
