using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Tenants.Commands;

public class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, RegisterTenantResult>
{
    private readonly IMasterDbContext _appDbContext;
    private readonly ITenantSchemaInitializer _schemaInitializer;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterTenantCommandHandler(
        IMasterDbContext appDbContext,
        ITenantSchemaInitializer schemaInitializer,
        IPasswordHasher passwordHasher)
    {
        _appDbContext = appDbContext;
        _schemaInitializer = schemaInitializer;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterTenantResult> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var slugInUse = await _appDbContext.Tenants.AnyAsync(t => t.Slug == request.Slug, cancellationToken);

        if (slugInUse)
        {
            throw new DomainException("El slug ya está en uso");
        }

        var tenant = Tenant.Create(request.Slug, request.BusinessName, request.Plan);

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

            var passwordHash = _passwordHasher.Hash(request.OwnerPassword);

            var owner = User.Create(
                $"{request.BusinessName} (Propietario)",
                request.OwnerEmail,
                passwordHash,
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
            _appDbContext.Tenants.Remove(tenant);
            await _appDbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return new RegisterTenantResult(tenant.Id, tenant.SchemaName, tenant.Slug, tenant.BusinessName);
    }
}

internal record RoleIdResult(Guid Id);
