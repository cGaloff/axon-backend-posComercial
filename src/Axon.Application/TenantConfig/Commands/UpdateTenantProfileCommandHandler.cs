using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.TenantConfig.Commands;

public class UpdateTenantProfileCommandHandler : IRequestHandler<UpdateTenantProfileCommand, UpdateTenantProfileResult>
{
    private readonly ITenantConfigRepository _tenantConfigRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMasterDbContext _masterDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantCacheInvalidator _tenantCacheInvalidator;

    public UpdateTenantProfileCommandHandler(
        ITenantConfigRepository tenantConfigRepository,
        IUnitOfWork unitOfWork,
        IMasterDbContext masterDbContext,
        ITenantContext tenantContext,
        ITenantCacheInvalidator tenantCacheInvalidator)
    {
        _tenantConfigRepository = tenantConfigRepository;
        _unitOfWork = unitOfWork;
        _masterDbContext = masterDbContext;
        _tenantContext = tenantContext;
        _tenantCacheInvalidator = tenantCacheInvalidator;
    }

    public async Task<UpdateTenantProfileResult> Handle(UpdateTenantProfileCommand request, CancellationToken cancellationToken)
    {
        var config = await _tenantConfigRepository.GetAsync()
            ?? throw new DomainException("Configuración del tenant no encontrada");

        config.UpdateProfile(
            request.BusinessName.Trim(),
            request.LegalName?.Trim(),
            request.Nit?.Trim(),
            request.Phone?.Trim(),
            request.Address?.Trim(),
            request.City?.Trim(),
            request.State?.Trim(),
            request.TaxRegime);

        _tenantConfigRepository.Update(config);
        await _unitOfWork.CommitAsync(cancellationToken);

        // Son dos bases distintas (schema del tenant y maestra), sin transacción
        // común: si esto falla, el perfil ya quedó guardado y el nombre del
        // login se corrige en el próximo guardado.
        var tenant = await _masterDbContext.Tenants
            .SingleOrDefaultAsync(t => t.Slug == _tenantContext.TenantSlug, cancellationToken);

        if (tenant is not null && tenant.BusinessName != config.BusinessName)
        {
            tenant.Rename(config.BusinessName);
            await _masterDbContext.SaveChangesAsync(cancellationToken);

            // El nombre que devuelve el login sale del tenant cacheado.
            _tenantCacheInvalidator.Invalidate(tenant.Slug);
        }

        return new UpdateTenantProfileResult(
            config.BusinessName,
            config.LegalName,
            config.Nit,
            config.Phone,
            config.Address,
            config.City,
            config.State,
            config.TaxRegime);
    }
}
