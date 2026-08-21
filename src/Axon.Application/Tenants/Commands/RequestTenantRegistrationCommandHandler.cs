using System.Security.Cryptography;
using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Axon.Application.Tenants.Commands;

public class RequestTenantRegistrationCommandHandler
    : IRequestHandler<RequestTenantRegistrationCommand, RequestTenantRegistrationResult>
{
    private readonly IMasterDbContext _masterDbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public RequestTenantRegistrationCommandHandler(
        IMasterDbContext masterDbContext,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _masterDbContext = masterDbContext;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<RequestTenantRegistrationResult> Handle(
        RequestTenantRegistrationCommand request, CancellationToken cancellationToken)
    {
        var slugInUse = await _masterDbContext.Tenants.AnyAsync(t => t.Slug == request.Slug, cancellationToken);

        if (slugInUse)
        {
            throw new DomainException("El slug ya está en uso");
        }

        var previousActiveRequests = await _masterDbContext.PendingTenantRegistrations
            .Where(p => p.OwnerEmail == request.OwnerEmail && p.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var previousRequest in previousActiveRequests)
        {
            previousRequest.MarkConsumed();
        }

        var ownerPasswordHash = _passwordHasher.Hash(request.OwnerPassword);

        var rawCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var expiresInMinutes = int.TryParse(_configuration["Auth:RegistrationCodeExpiresInMinutes"], out var minutes)
            ? minutes
            : 15;

        var pendingRegistration = PendingTenantRegistration.Create(
            request.BusinessName,
            request.Slug,
            request.OwnerEmail,
            ownerPasswordHash,
            request.Plan,
            PendingTenantRegistration.HashVerificationCode(rawCode),
            DateTime.UtcNow.AddMinutes(expiresInMinutes));

        _masterDbContext.PendingTenantRegistrations.Add(pendingRegistration);
        await _masterDbContext.SaveChangesAsync(cancellationToken);

        await _emailService.SendRegistrationVerificationCodeAsync(
            request.OwnerEmail, request.BusinessName, rawCode);

        return new RequestTenantRegistrationResult(pendingRegistration.Id);
    }
}
