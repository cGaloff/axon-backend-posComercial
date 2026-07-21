using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Auth.Commands;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<MediatRUnit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshToken.HashToken(request.RefreshToken);

        var storedToken = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        // Idempotente a propósito: si el token ya no existe o ya estaba revocado,
        // el objetivo del logout (que ese token no sirva más) ya se cumple.
        if (storedToken is not null && storedToken.IsActive)
        {
            storedToken.Revoke();
            await _unitOfWork.CommitAsync(cancellationToken);
        }

        return MediatRUnit.Value;
    }
}
