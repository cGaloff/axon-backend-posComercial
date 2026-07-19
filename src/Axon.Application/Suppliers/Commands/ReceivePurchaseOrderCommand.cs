using MediatR;

namespace Axon.Application.Suppliers.Commands;

public record ReceivePurchaseOrderCommand(
    Guid PurchaseOrderId,
    List<ReceiptItemRequest> Items,
    string? Notes) : IRequest<ReceivePurchaseOrderResult>;

public record ReceiptItemRequest(
    Guid PurchaseOrderItemId,
    int QuantityReceived);

public record ReceivePurchaseOrderResult(
    Guid ReceiptId,
    decimal TotalReceived,
    string OrderStatus);
