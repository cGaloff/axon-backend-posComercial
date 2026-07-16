using Axon.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Queries;

public class GetUnitsQueryHandler : IRequestHandler<GetUnitsQuery, List<UnitDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetUnitsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<UnitDto>> Handle(GetUnitsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Units
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new UnitDto(u.Id, u.Name, u.Abbreviation, u.IsActive))
            .ToListAsync(cancellationToken);
    }
}
