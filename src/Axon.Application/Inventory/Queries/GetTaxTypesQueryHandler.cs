using Axon.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Queries;

public class GetTaxTypesQueryHandler : IRequestHandler<GetTaxTypesQuery, List<TaxTypeDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetTaxTypesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TaxTypeDto>> Handle(GetTaxTypesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.TaxTypes.AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query
            .OrderBy(t => t.Name)
            .Select(t => new TaxTypeDto(t.Id, t.Name, t.Code, t.IsActive))
            .ToListAsync(cancellationToken);
    }
}
