using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;

namespace Axon.Application.CashRegister.Commands;

public class CloseCashSessionCommandHandler : IRequestHandler<CloseCashSessionCommand, CloseCashSessionResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICashSessionRepository _cashSessionRepository;

    public CloseCashSessionCommandHandler(IUnitOfWork unitOfWork, ICashSessionRepository cashSessionRepository)
    {
        _unitOfWork = unitOfWork;
        _cashSessionRepository = cashSessionRepository;
    }

    public async Task<CloseCashSessionResult> Handle(CloseCashSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _cashSessionRepository.GetByIdAsync(request.SessionId);

        if (session is null)
        {
            throw new DomainException("La sesión no existe");
        }

        // TODO: validar que ClosedBy tiene rol
        // Admin o Propietario cuando exista ICurrentUserContext.
        // Actualmente cualquier usuario autenticado puede
        // forzar el cierre — gap de seguridad conocido.
        if (request.ForceClose)
        {
            session.ForceClose(request.ClosedBy, request.Notes ?? string.Empty);
        }
        else
        {
            // Verificación de rol (Admin/Propietario) diferida hasta que exista un
            // mecanismo de permisos por rol: por ahora solo se exige que quien
            // cierra sea quien abrió la sesión.
            if (request.ClosedBy != session.OpenedBy)
            {
                throw new DomainException("Solo el cajero que abrió la sesión puede cerrarla");
            }

            session.Close(request.ClosedBy, request.CountedAmount, request.Notes);
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
