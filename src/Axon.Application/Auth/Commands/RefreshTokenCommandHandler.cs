using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Auth.Commands;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public RefreshTokenCommandHandler(
        IApplicationDbContext dbContext,
        IJwtTokenService jwtTokenService,
        ITenantContext tenantContext,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task<LoginResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshToken.HashToken(request.RefreshToken);

        var storedToken = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || !storedToken.IsActive)
        {
            throw new DomainException("El refresh token es inválido o expiró. Iniciá sesión de nuevo.");
        }

        var user = await _dbContext.Users
            .Include(u => u.Role)
            .ThenInclude(r => r!.Permissions)
            .SingleOrDefaultAsync(u => u.Id == storedToken.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new DomainException("El usuario ya no está activo");
        }

        // Rotación: el refresh token usado queda inservible aunque no haya expirado,
        // así que si alguien lo intercepta y lo reusa después de un refresh legítimo,
        // ya no le sirve.
        storedToken.Revoke();

        var result = _jwtTokenService.GenerateToken(user, _tenantContext);

        var refreshTokenDays = int.TryParse(_configuration["Jwt:RefreshTokenExpiresInDays"], out var days) ? days : 7;
        var newRefreshToken = RefreshToken.Create(
            user.Id,
            RefreshToken.HashToken(result.RefreshToken),
            DateTime.UtcNow.AddDays(refreshTokenDays));

        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return result;
    }
}
