using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Queries;

public class GetProductBarcodeQueryHandler : IRequestHandler<GetProductBarcodeQuery, byte[]>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IBarcodeService _barcodeService;

    public GetProductBarcodeQueryHandler(IApplicationDbContext dbContext, IBarcodeService barcodeService)
    {
        _dbContext = dbContext;
        _barcodeService = barcodeService;
    }

    public async Task<byte[]> Handle(GetProductBarcodeQuery request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.SingleOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            throw new DomainException("Producto no encontrado");
        }

        return _barcodeService.GenerateBarcode(product.Sku);
    }
}
