using Axon.Application.Common.Models;
using Axon.Domain.Entities.Suppliers;
using MediatR;

namespace Axon.Application.Suppliers.Queries;

public record GetPurchaseOrdersQuery(
    Guid? SupplierId = null,
    PurchaseOrderStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<PurchaseOrderDto>>;

public record PurchaseOrderDto(
    Guid Id,
    string SupplierName,
    string Status,
    decimal TotalOrdered,
    DateTime OrderDate,
    DateTime? ExpectedDate,
    int ItemCount,
    int PendingItemsCount);
