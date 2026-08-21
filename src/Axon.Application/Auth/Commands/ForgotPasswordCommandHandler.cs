using System.Security.Cryptography;
using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Auth.Commands;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public ForgotPasswordCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<MediatRUnit> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        // No se distingue entre "no existe" y "existe" en la respuesta (ni acá
        // arriba, ni en el controller): permitir esa distinción es lo que
        // habilitaría enumerar correos registrados a través de este endpoint.
        if (user is null || !user.IsActive)
        {
            return MediatRUnit.Value;
        }

        var previousActiveTokens = await _dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var previousToken in previousActiveTokens)
        {
            previousToken.MarkUsed();
        }

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresInMinutes = int.TryParse(_configuration["Auth:PasswordResetTokenExpiresInMinutes"], out var minutes)
            ? minutes
            : 30;

        var resetToken = PasswordResetToken.Create(
            user.Id,
            PasswordResetToken.HashToken(rawToken),
            DateTime.UtcNow.AddMinutes(expiresInMinutes));

        _dbContext.PasswordResetTokens.Add(resetToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var baseUrl = _configuration["Frontend:ResetPasswordUrl"] ?? string.Empty;
        var resetLink = $"{baseUrl}?token={rawToken}&tenant={request.TenantSlug}";

        await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, resetLink);

        return MediatRUnit.Value;
    }
}
