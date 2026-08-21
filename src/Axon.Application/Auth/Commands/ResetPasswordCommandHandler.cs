using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Auth.Commands;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<MediatRUnit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = PasswordResetToken.HashToken(request.Token);
        var resetToken = await _dbContext.PasswordResetTokens
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (resetToken is null || !resetToken.IsActive)
        {
            throw new DomainException("El enlace de recuperación no es válido o expiró.");
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(u => u.Id == resetToken.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new DomainException("El enlace de recuperación no es válido o expiró.");
        }

        user.ResetPassword(_passwordHasher.Hash(request.NewPassword));
        resetToken.MarkUsed();

        var activeRefreshTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.Revoke();
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
