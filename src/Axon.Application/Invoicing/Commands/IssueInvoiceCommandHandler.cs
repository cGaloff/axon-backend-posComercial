using Axon.Application.Interfaces;
using Axon.Domain.Entities.Invoicing;
using Axon.Domain.Entities.Sales;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Invoicing.Commands;

public class IssueInvoiceCommandHandler : IRequestHandler<IssueInvoiceCommand, IssueInvoiceResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPdfService _pdfService;
    private readonly ITenantConfigRepository _tenantConfigRepository;

    public IssueInvoiceCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        IPdfService pdfService,
        ITenantConfigRepository tenantConfigRepository)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _pdfService = pdfService;
        _tenantConfigRepository = tenantConfigRepository;
    }

    public async Task<IssueInvoiceResult> Handle(IssueInvoiceCommand request, CancellationToken cancellationToken)
    {
        var sale = await _dbContext.Sales
            .Include(s => s.Items)
            .SingleOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale is null)
        {
            throw new DomainException("La venta no existe");
        }

        if (sale.Status != SaleStatus.Completed)
        {
            throw new DomainException("Solo se puede facturar una venta completada.");
        }

        var config = await _tenantConfigRepository.GetAsync()
            ?? throw new DomainException("Configuración del tenant no encontrada");

        // Idempotente: si el evento se dispara más de una vez para la misma venta
        // (p. ej. un webhook de pago reintentado por el proveedor), se devuelve la
        // factura ya emitida en vez de fallar o duplicarla.
        var existingInvoice = await _dbContext.Invoices
            .SingleOrDefaultAsync(i => i.SaleId == sale.Id, cancellationToken);

        if (existingInvoice is not null)
        {
            var existingPdf = _pdfService.GenerateSaleReceipt(sale, config);
            return new IssueInvoiceResult(existingInvoice.Id, existingInvoice.Number, existingPdf);
        }

        var number = await _dbContext.GetNextInvoiceNumberAsync(cancellationToken);

        var itemSnapshots = sale.Items.Select(item => new InvoiceItemSnapshot(
            item.ProductId,
            item.ProductName,
            item.ProductSku,
            item.UnitPrice,
            item.Quantity,
            item.Discount,
            item.Subtotal,
            item.SubtotalBase,
            item.Taxes.Select(t => new InvoiceItemTaxSnapshot(t.TaxTypeId, t.TaxTypeName, t.Percentage, t.Amount)).ToList()))
            .ToList();

        var paymentSnapshots = sale.Payments
            .Select(p => new InvoicePaymentSnapshot(p.Method, p.Amount, p.AmountTendered, p.Change))
            .ToList();

        var invoice = Invoice.Create(
            sale.Id,
            number,
            sale.SaleNumber,
            sale.CustomerName,
            sale.Total,
            itemSnapshots,
            paymentSnapshots);

        _dbContext.Invoices.Add(invoice);
        await _unitOfWork.CommitAsync(cancellationToken);

        // Reutiliza el mismo servicio de PDF del recibo de venta (no un formato
        // de factura distinto): la factura es el registro auditable, el PDF es
        // la misma salida impresa que ya existía.
        var pdf = _pdfService.GenerateSaleReceipt(sale, config);

        return new IssueInvoiceResult(invoice.Id, invoice.Number, pdf);
    }
}
