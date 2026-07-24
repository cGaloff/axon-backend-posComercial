using Axon.Domain.Entities.Suppliers;
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
    string? SupplierInvoiceNumber,
    DateTime? SupplierInvoiceDate,
    SupplierDocumentType SupplierDocumentTypeAtPurchase,
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
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total,
    List<PurchaseOrderItemTaxDto> Taxes);

public record PurchaseOrderItemTaxDto(Guid TaxTypeId, string TaxTypeName, decimal Percentage, decimal Amount);
