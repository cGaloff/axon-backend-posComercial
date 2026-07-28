using Axon.Application.Common.Models;
using Axon.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Audit.Queries;

public class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, PagedResult<AuditLogDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAuditLogQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (request.From.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= request.To.Value);
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(a => a.UserId == request.UserId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = logs.Select(a => a.UserId).Distinct().ToList();

        var userNames = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var items = logs
            .Select(a => new AuditLogDto(
                a.Id,
                a.UserId,
                userNames.GetValueOrDefault(a.UserId, "(usuario no encontrado)"),
                a.Action,
                a.EntityId,
                a.CreatedAt))
            .ToList();

        return new PagedResult<AuditLogDto>(totalCount, request.Page, request.PageSize, items);
    }
}
