using MediatR;

namespace Axon.Application.Suppliers.Commands;

public record CreatePurchaseOrderCommand(
    Guid SupplierId,
    List<PurchaseOrderItemRequest> Items,
    string? SupplierInvoiceNumber,
    DateTime? SupplierInvoiceDate,
    DateTime? ExpectedDate,
    string? Notes) : IRequest<Guid>;

public record PurchaseOrderItemRequest(
    Guid ProductId,
    int QuantityOrdered,
    decimal UnitCost);
