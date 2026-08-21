using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Axon.Application.Tenants.Commands;

public class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, RegisterTenantResult>
{
    private readonly IMasterDbContext _appDbContext;
    private readonly ITenantSchemaInitializer _schemaInitializer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RegisterTenantCommandHandler> _logger;

    public RegisterTenantCommandHandler(
        IMasterDbContext appDbContext,
        ITenantSchemaInitializer schemaInitializer,
        IConfiguration configuration,
        ILogger<RegisterTenantCommandHandler> logger)
    {
        _appDbContext = appDbContext;
        _schemaInitializer = schemaInitializer;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RegisterTenantResult> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var slugInUse = await _appDbContext.Tenants.AnyAsync(t => t.Slug == request.Slug, cancellationToken);

        if (slugInUse)
        {
            throw new DomainException("El slug ya está en uso");
        }

        var trialDays = int.TryParse(_configuration["Subscription:TrialDays"], out var days) ? days : 7;

        var tenant = Tenant.Create(
            request.Slug, request.BusinessName, request.Plan,
            request.OwnerEmail, DateTime.UtcNow.AddDays(trialDays));

        _appDbContext.Tenants.Add(tenant);
        await _appDbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _schemaInitializer.InitializeSchemaAsync(tenant.SchemaName);

            var propietarioRoleId = await _appDbContext.Database
                .SqlQueryRaw<RoleIdResult>($"SELECT id FROM \"{tenant.SchemaName}\".roles WHERE name = 'Propietario' LIMIT 1")
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (propietarioRoleId == Guid.Empty)
            {
                throw new DomainException("No se encontró el rol 'Propietario' en el schema del tenant recién creado");
            }

            var owner = User.Create(
                $"{request.BusinessName} (Propietario)",
                request.OwnerEmail,
                request.OwnerPasswordHash,
                propietarioRoleId);

            var insertUserSql =
                $"INSERT INTO \"{tenant.SchemaName}\".users (id, full_name, email, password_hash, role_id, is_active, created_at) " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})";

            await _appDbContext.Database.ExecuteSqlRawAsync(
                insertUserSql,
                new object[] { owner.Id, owner.FullName, owner.Email, owner.PasswordHash, owner.RoleId, owner.IsActive, owner.CreatedAt },
                cancellationToken);
        }
        catch
        {
            try
            {
                await _schemaInitializer.DropSchemaAsync(tenant.SchemaName);
            }
            catch (Exception cleanupEx)
            {
                // No se debe enmascarar el error original de aprovisionamiento por uno
                // de limpieza; el schema queda huerfano para revisión/DROP manual.
                _logger.LogError(
                    cleanupEx,
                    "No se pudo eliminar el schema huérfano '{SchemaName}' tras un registro de tenant fallido.",
                    tenant.SchemaName);
            }

            _appDbContext.Tenants.Remove(tenant);
            await _appDbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return new RegisterTenantResult(tenant.Id, tenant.SchemaName, tenant.Slug, tenant.BusinessName);
    }
}

internal record RoleIdResult(Guid Id);
