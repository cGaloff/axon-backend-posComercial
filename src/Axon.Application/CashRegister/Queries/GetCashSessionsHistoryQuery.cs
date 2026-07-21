using Axon.Application.Common.Models;
using Axon.Domain.Entities.CashRegister;
using MediatR;

namespace Axon.Application.CashRegister.Queries;

public record GetCashSessionsHistoryQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    Guid? CashRegisterId = null,
    CashSessionStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<CashSessionSummaryDto>>;
