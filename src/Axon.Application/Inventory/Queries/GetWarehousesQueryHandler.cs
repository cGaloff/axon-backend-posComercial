using Axon.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Queries;

public class GetWarehousesQueryHandler : IRequestHandler<GetWarehousesQuery, List<WarehouseDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetWarehousesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<WarehouseDto>> Handle(GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Warehouses
            .Where(w => w.IsActive)
            .OrderBy(w => w.Name)
            .Select(w => new WarehouseDto(w.Id, w.Name, w.Description, w.IsDefault, w.IsActive))
            .ToListAsync(cancellationToken);
    }
}
