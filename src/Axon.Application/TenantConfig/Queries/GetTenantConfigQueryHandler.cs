using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;

namespace Axon.Application.TenantConfig.Queries;

public class GetTenantConfigQueryHandler : IRequestHandler<GetTenantConfigQuery, TenantConfigDto>
{
    private readonly ITenantConfigRepository _tenantConfigRepository;

    public GetTenantConfigQueryHandler(ITenantConfigRepository tenantConfigRepository)
    {
        _tenantConfigRepository = tenantConfigRepository;
    }

    public async Task<TenantConfigDto> Handle(GetTenantConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _tenantConfigRepository.GetAsync()
            ?? throw new DomainException("Configuración del tenant no encontrada");

        return new TenantConfigDto(
            config.Id,
            config.BusinessName,
            config.Nit,
            config.Address,
            config.Phone,
            config.Email,
            config.Website,
            config.LogoUrl,
            config.IsResponsableIva);
    }
}
