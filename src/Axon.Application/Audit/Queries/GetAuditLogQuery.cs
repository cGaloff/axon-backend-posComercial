using Axon.Application.Common.Models;
using MediatR;

namespace Axon.Application.Audit.Queries;

public record GetAuditLogQuery(
    DateTime? From = null,
    DateTime? To = null,
    Guid? UserId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<AuditLogDto>>;

public record AuditLogDto(Guid Id, Guid UserId, string UserFullName, string Action, Guid? EntityId, DateTime CreatedAt);
