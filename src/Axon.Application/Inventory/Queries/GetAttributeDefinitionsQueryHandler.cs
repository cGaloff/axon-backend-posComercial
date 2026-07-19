using Axon.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Queries;

public class GetAttributeDefinitionsQueryHandler : IRequestHandler<GetAttributeDefinitionsQuery, List<AttributeDefinitionDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetAttributeDefinitionsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AttributeDefinitionDto>> Handle(GetAttributeDefinitionsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AttributeDefinitions.Where(a => a.IsActive);

        if (request.CategoryId.HasValue)
        {
            query = query.Where(a => a.CategoryId == null || a.CategoryId == request.CategoryId.Value);
        }

        return await query
            .OrderBy(a => a.SortOrder)
            .Select(a => new AttributeDefinitionDto(
                a.Id, a.Key, a.Label, a.Type, a.Options, a.CategoryId, a.IsFilterable, a.SortOrder))
            .ToListAsync(cancellationToken);
    }
}
