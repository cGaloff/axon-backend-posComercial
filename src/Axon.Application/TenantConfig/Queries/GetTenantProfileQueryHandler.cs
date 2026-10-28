using Axon.Application.Interfaces;
using Axon.Domain.Entities;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.TenantConfig.Queries;

public class GetTenantProfileQueryHandler : IRequestHandler<GetTenantProfileQuery, TenantProfileDto>
{
    private readonly ITenantConfigRepository _tenantConfigRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly IMasterDbContext _masterDbContext;
    private readonly ITenantContext _tenantContext;

    public GetTenantProfileQueryHandler(
        ITenantConfigRepository tenantConfigRepository,
        IApplicationDbContext dbContext,
        IMasterDbContext masterDbContext,
        ITenantContext tenantContext)
    {
        _tenantConfigRepository = tenantConfigRepository;
        _dbContext = dbContext;
        _masterDbContext = masterDbContext;
        _tenantContext = tenantContext;
    }

    public async Task<TenantProfileDto> Handle(GetTenantProfileQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _masterDbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Slug == _tenantContext.TenantSlug, cancellationToken)
            ?? throw new DomainException("Tenant no encontrado");

        var config = await _tenantConfigRepository.GetAsync()
            ?? throw new DomainException("Configuración del tenant no encontrada");

        // El Propietario más antiguo es quien registró el negocio.
        var ownerName = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Role!.Name == Role.OwnerRoleName && u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        return new TenantProfileDto(
            tenant.Id,
            config.BusinessName,
            config.LegalName,
            config.Nit,
            config.Phone,
            config.Address,
            config.City,
            config.State,
            config.TaxRegime,
            config.Email ?? tenant.OwnerEmail,
            tenant.Slug,
            ownerName,
            tenant.CreatedAt);
    }
}
