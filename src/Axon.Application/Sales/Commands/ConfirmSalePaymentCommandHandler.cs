using Axon.Application.Interfaces;
using Axon.Application.Invoicing.Commands;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Sales.Commands;

public class ConfirmSalePaymentCommandHandler : IRequestHandler<ConfirmSalePaymentCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;

    public ConfirmSalePaymentCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork, IMediator mediator)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _mediator = mediator;
    }

    public async Task<MediatRUnit> Handle(ConfirmSalePaymentCommand request, CancellationToken cancellationToken)
    {
        var sale = await _dbContext.Sales.SingleOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale is null)
        {
            throw new DomainException("La venta no existe");
        }

        sale.Complete();

        await _unitOfWork.CommitAsync(cancellationToken);

        // Mismo evento de "pago exitoso" que en ProcessSaleCommandHandler, para
        // ventas que quedaron pendientes de confirmación externa (tarjeta/
        // transferencia): ahora se emite la factura (PDF + registro Invoice).
        await _mediator.Send(new IssueInvoiceCommand(sale.Id), cancellationToken);

        return MediatRUnit.Value;
    }
}
