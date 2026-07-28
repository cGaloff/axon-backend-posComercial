using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Interfaces;
using MediatR;

namespace Axon.Application.Common.Behaviors;

// Registrado DESPUÉS de ValidationBehavior (más cerca del handler real): si la
// validación o el propio handler lanzan una excepción, next() propaga esa
// excepción ANTES de llegar al registro de auditoría de abajo — solo se audita
// una acción que realmente se completó con éxito, nunca un intento fallido o
// rechazado (Matriz de Roles y Permisos v2, regla transversal A).
public class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public AuditLoggingBehavior(
        IApplicationDbContext dbContext, IUnitOfWork unitOfWork, ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        if (request is IAuditableRequest auditable)
        {
            var entry = AuditLog.Create(_currentUserContext.UserId, auditable.AuditAction, auditable.AuditEntityId);
            _dbContext.AuditLogs.Add(entry);

            // Commit separado del que ya hizo el propio handler dentro de next():
            // si este INSERT fallara, la acción de negocio ya se completó y no debe
            // revertirse solo porque el log no pudo escribirse.
            await _unitOfWork.CommitAsync(cancellationToken);
        }

        return response;
    }
}
