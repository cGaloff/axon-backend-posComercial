using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;

namespace Axon.Application.CashRegister.Commands;

public class CloseCashSessionCommandHandler : IRequestHandler<CloseCashSessionCommand, CloseCashSessionResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ICurrentUserContext _currentUserContext;

    public CloseCashSessionCommandHandler(
        IUnitOfWork unitOfWork,
        ICashSessionRepository cashSessionRepository,
        ICurrentUserContext currentUserContext)
    {
        _unitOfWork = unitOfWork;
        _cashSessionRepository = cashSessionRepository;
        _currentUserContext = currentUserContext;
    }

    public async Task<CloseCashSessionResult> Handle(CloseCashSessionCommand request, CancellationToken cancellationToken)
    {
        var closedBy = _currentUserContext.UserId;

        var session = await _cashSessionRepository.GetByIdAsync(request.SessionId);

        if (session is null)
        {
            throw new DomainException("La sesión no existe");
        }

        if (request.ForceClose)
        {
            // Gap de seguridad resuelto: solo Propietario o Administrador puede forzar el cierre.
            if (!_currentUserContext.IsInRole("Propietario") && !_currentUserContext.IsInRole("Administrador"))
            {
                throw new DomainException("Solo el Propietario o Administrador puede forzar el cierre de caja");
            }

            session.ForceClose(closedBy, request.Notes ?? string.Empty);
        }
        else
        {
            if (closedBy != session.OpenedBy)
            {
                throw new DomainException("Solo el cajero que abrió la sesión puede cerrarla");
            }

            session.Close(closedBy, request.CountedAmount, request.Notes);
        }

        _cashSessionRepository.Update(session);

        await _unitOfWork.CommitAsync(cancellationToken);

        return new CloseCashSessionResult(
            session.Id,
            session.ExpectedAmount,
            session.CountedAmount ?? 0,
            session.Difference ?? 0,
            session.Status.ToString(),
            session.ClosedAt ?? DateTime.UtcNow);
    }
}
