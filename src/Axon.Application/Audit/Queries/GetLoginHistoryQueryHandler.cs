using Axon.Application.Common.Models;
using Axon.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Audit.Queries;

public class GetLoginHistoryQueryHandler : IRequestHandler<GetLoginHistoryQuery, PagedResult<LoginAttemptDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetLoginHistoryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<LoginAttemptDto>> Handle(GetLoginHistoryQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.LoginAttempts.AsQueryable();

        if (request.From.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= request.To.Value);
        }

        if (request.SuccessOnly.HasValue)
        {
            query = query.Where(a => a.Success == request.SuccessOnly.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new LoginAttemptDto(a.Id, a.Email, a.UserId, a.Success, a.IpAddress, a.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<LoginAttemptDto>(totalCount, request.Page, request.PageSize, items);
    }
}
