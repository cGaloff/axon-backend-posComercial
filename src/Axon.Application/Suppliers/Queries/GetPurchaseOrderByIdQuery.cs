using MediatR;

namespace Axon.Application.Suppliers.Queries;

public record GetPurchaseOrderByIdQuery(Guid Id) : IRequest<PurchaseOrderDetailsDto>;

public record PurchaseOrderDetailsDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string Status,
    string? Notes,
    DateTime OrderDate,
    DateTime? ExpectedDate,
    decimal TotalOrdered,
    List<PurchaseOrderItemDetailsDto> Items);

public record PurchaseOrderItemDetailsDto(
    Guid PurchaseOrderItemId,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    int QuantityOrdered,
    int QuantityReceived,
    decimal UnitCost,
    decimal Subtotal);
