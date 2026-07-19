using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Sales.Queries;

public class GetSaleReceiptQueryHandler : IRequestHandler<GetSaleReceiptQuery, byte[]>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPdfService _pdfService;
    private readonly ITenantConfigRepository _tenantConfigRepository;

    public GetSaleReceiptQueryHandler(
        IApplicationDbContext dbContext,
        IPdfService pdfService,
        ITenantConfigRepository tenantConfigRepository)
    {
        _dbContext = dbContext;
        _pdfService = pdfService;
        _tenantConfigRepository = tenantConfigRepository;
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

        var config = await _tenantConfigRepository.GetAsync()
            ?? throw new DomainException("Configuración del tenant no encontrada");

        return _pdfService.GenerateSaleReceipt(sale, config);
    }
}
