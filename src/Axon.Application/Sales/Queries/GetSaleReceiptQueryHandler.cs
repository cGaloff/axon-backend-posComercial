using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Sales.Queries;

public class GetSaleReceiptQueryHandler : IRequestHandler<GetSaleReceiptQuery, byte[]>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPdfService _pdfService;
    private readonly IConfiguration _configuration;

    public GetSaleReceiptQueryHandler(IApplicationDbContext dbContext, IPdfService pdfService, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _pdfService = pdfService;
        _configuration = configuration;
    }

    public async Task<byte[]> Handle(GetSaleReceiptQuery request, CancellationToken cancellationToken)
    {
        var sale = await _dbContext.Sales
            .Include(s => s.Items)
            .SingleOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale is null)
        {
            throw new DomainException("La venta no existe");
        }

        var businessName = _configuration["BusinessName"] ?? "Axon POS";

        return _pdfService.GenerateSaleReceipt(sale, businessName);
    }
}
