using Axon.Application.Common.Models;
using MediatR;

namespace Axon.Application.Audit.Queries;

public record GetLoginHistoryQuery(
    DateTime? From = null,
    DateTime? To = null,
    bool? SuccessOnly = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<LoginAttemptDto>>;

public record LoginAttemptDto(Guid Id, string Email, Guid? UserId, bool Success, string? IpAddress, DateTime CreatedAt);
