using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Suppliers.Queries;

public class GetSupplierAccountStatementQueryHandler : IRequestHandler<GetSupplierAccountStatementQuery, SupplierAccountStatementDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetSupplierAccountStatementQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SupplierAccountStatementDto> Handle(GetSupplierAccountStatementQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers.SingleOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);

        if (supplier is null)
        {
            throw new DomainException("El proveedor no existe");
        }

        var receiptsQuery = _dbContext.PurchaseReceipts
            .Join(_dbContext.PurchaseOrders, r => r.PurchaseOrderId, o => o.Id, (r, o) => new { Receipt = r, o.SupplierId })
            .Where(x => x.SupplierId == request.SupplierId);

        if (request.From.HasValue)
        {
            receiptsQuery = receiptsQuery.Where(x => x.Receipt.ReceivedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            receiptsQuery = receiptsQuery.Where(x => x.Receipt.ReceivedAt <= request.To.Value);
        }

        var receipts = await receiptsQuery
            .Select(x => new { x.Receipt.ReceivedAt, x.Receipt.TotalReceived })
            .ToListAsync(cancellationToken);

        var paymentsQuery = _dbContext.SupplierPayments.Where(p => p.SupplierId == request.SupplierId);

        if (request.From.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.PaidAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.PaidAt <= request.To.Value);
        }

        var payments = await paymentsQuery
            .Select(p => new { p.PaidAt, p.Amount })
            .ToListAsync(cancellationToken);

        // Las compras suman al saldo (lo que se debe); los pagos restan. RunningBalance
        // es una suma acumulada simple sobre Amount ya firmado.
        var transactions = receipts
            .Select(r => new { Date = r.ReceivedAt, Type = "Purchase", Description = "Recepción de mercancía", Amount = r.TotalReceived })
            .Concat(payments.Select(p => new { Date = p.PaidAt, Type = "Payment", Description = "Pago a proveedor", Amount = -p.Amount }))
            .OrderBy(t => t.Date)
            .ToList();

        var runningBalance = 0m;
        var transactionDtos = new List<SupplierTransactionDto>();

        foreach (var t in transactions)
        {
            runningBalance += t.Amount;
            transactionDtos.Add(new SupplierTransactionDto(t.Date, t.Type, t.Description, t.Amount, runningBalance));
        }

        var totalPurchased = receipts.Sum(r => r.TotalReceived);
        var totalPaid = payments.Sum(p => p.Amount);

        return new SupplierAccountStatementDto(
            supplier.Id,
            supplier.Name,
            totalPurchased,
            totalPaid,
            totalPurchased - totalPaid,
            transactionDtos);
    }
}
