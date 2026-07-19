using Axon.Application.Interfaces;
using Axon.Domain.Entities.Suppliers;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Suppliers.Commands;

public class RegisterSupplierPaymentCommandHandler : IRequestHandler<RegisterSupplierPaymentCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public RegisterSupplierPaymentCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    public async Task<Guid> Handle(RegisterSupplierPaymentCommand request, CancellationToken cancellationToken)
    {
        var supplierExists = await _dbContext.Suppliers.AnyAsync(s => s.Id == request.SupplierId, cancellationToken);

        if (!supplierExists)
        {
            throw new DomainException("El proveedor no existe");
        }

        var payment = SupplierPayment.Create(
            request.SupplierId,
            request.Amount,
            request.PaymentMethod,
            _currentUserContext.UserId,
            request.Reference,
            request.Notes);

        _dbContext.SupplierPayments.Add(payment);
        await _unitOfWork.CommitAsync(cancellationToken);

        return payment.Id;
    }
}
