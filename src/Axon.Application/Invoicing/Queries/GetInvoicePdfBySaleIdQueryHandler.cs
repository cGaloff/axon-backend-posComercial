using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Invoicing.Queries;

public class GetInvoicePdfBySaleIdQueryHandler : IRequestHandler<GetInvoicePdfBySaleIdQuery, byte[]>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPdfService _pdfService;
    private readonly ITenantConfigRepository _tenantConfigRepository;

    public GetInvoicePdfBySaleIdQueryHandler(
        IApplicationDbContext dbContext,
        IPdfService pdfService,
        ITenantConfigRepository tenantConfigRepository)
    {
        _dbContext = dbContext;
        _pdfService = pdfService;
        _tenantConfigRepository = tenantConfigRepository;
    }

    public async Task<byte[]> Handle(GetInvoicePdfBySaleIdQuery request, CancellationToken cancellationToken)
    {
        var invoiceExists = await _dbContext.Invoices.AnyAsync(i => i.SaleId == request.SaleId, cancellationToken);

        if (!invoiceExists)
        {
            throw new DomainException("Esta venta todavía no tiene una factura emitida.");
        }

        var sale = await _dbContext.Sales
            .Include(s => s.Items)
            .SingleOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale is null)
        {
            throw new DomainException("La venta no existe");
        }

        var config = await _tenantConfigRepository.GetAsync()
            ?? throw new DomainException("Configuración del tenant no encontrada");

        // Reutiliza el mismo servicio de PDF ya usado al emitir la factura (ver
        // IssueInvoiceCommandHandler): la venta ya no cambia después de completarse,
        // así que regenerarlo aquí produce el mismo contenido que el original.
        return _pdfService.GenerateSaleReceipt(sale, config);
    }
}
