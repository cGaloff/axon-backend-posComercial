using Axon.Application.Common.Models;
using Axon.Application.Sales.DTOs;
using Axon.Domain.Entities.Sales;
using MediatR;

namespace Axon.Application.Sales.Queries;

public record GetSalesHistoryQuery(
    DateTime? From,
    DateTime? To,
    SaleStatus? Status,
    Guid? CustomerId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<SaleDto>>;
