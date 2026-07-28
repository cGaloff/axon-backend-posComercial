using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Auth.Commands;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public LoginCommandHandler(
        IApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ITenantContext tenantContext,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.Role)
            .ThenInclude(r => r!.Permissions)
            .SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null)
        {
            // No hay a quién atribuirlo, pero igual queda constancia del intento
            // (Matriz de Roles y Permisos v2, "seguridad técnica").
            await RecordAttemptAsync(request.Email, userId: null, success: false, request.IpAddress, cancellationToken);
            throw new DomainException("Credenciales inválidas");
        }

        if (!user.IsActive)
        {
            await RecordAttemptAsync(request.Email, user.Id, success: false, request.IpAddress, cancellationToken);
            throw new DomainException("Usuario inactivo");
        }

        if (user.IsLockedOut)
        {
            await RecordAttemptAsync(request.Email, user.Id, success: false, request.IpAddress, cancellationToken);
            throw new DomainException(
                $"Cuenta bloqueada temporalmente por demasiados intentos fallidos. Intente de nuevo después de {user.LockedUntil:HH:mm}.");
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // Se registra el intento fallido ANTES de lanzar la excepción — si no
            // se guardara aquí, un atacante podría intentar indefinidamente sin
            // que el contador avanzara nunca.
            user.RegisterFailedLoginAttempt();
            await RecordAttemptAsync(request.Email, user.Id, success: false, request.IpAddress, cancellationToken);
            throw new DomainException("Credenciales inválidas");
        }

        user.RegisterSuccessfulLogin();
        await RecordAttemptAsync(request.Email, user.Id, success: true, request.IpAddress, cancellationToken);

        var result = _jwtTokenService.GenerateToken(user, _tenantContext);

        var refreshTokenDays = int.TryParse(_configuration["Jwt:RefreshTokenExpiresInDays"], out var days) ? days : 7;
        var refreshToken = RefreshToken.Create(
            user.Id,
            RefreshToken.HashToken(result.RefreshToken),
            DateTime.UtcNow.AddDays(refreshTokenDays));

        _dbContext.RefreshTokens.Add(refreshToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return result;
    }

    // Guarda el intento Y el estado de bloqueo/contador del usuario (si aplica)
    // en el mismo commit: ambos deben quedar persistidos aunque el flujo termine
    // lanzando una excepción justo después.
    private async Task RecordAttemptAsync(
        string email, Guid? userId, bool success, string? ipAddress, CancellationToken cancellationToken)
    {
        var attempt = LoginAttempt.Create(email, userId, success, ipAddress);
        _dbContext.LoginAttempts.Add(attempt);
        await _unitOfWork.CommitAsync(cancellationToken);
    }
}
