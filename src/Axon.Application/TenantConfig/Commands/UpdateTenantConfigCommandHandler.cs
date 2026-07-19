using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.TenantConfig.Commands;

public class UpdateTenantConfigCommandHandler : IRequestHandler<UpdateTenantConfigCommand, MediatRUnit>
{
    private readonly ITenantConfigRepository _tenantConfigRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTenantConfigCommandHandler(ITenantConfigRepository tenantConfigRepository, IUnitOfWork unitOfWork)
    {
        _tenantConfigRepository = tenantConfigRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MediatRUnit> Handle(UpdateTenantConfigCommand request, CancellationToken cancellationToken)
    {
        var config = await _tenantConfigRepository.GetAsync()
            ?? throw new DomainException("Configuración del tenant no encontrada");

        config.Update(
            request.BusinessName,
            request.Nit,
            request.Address,
            request.Phone,
            request.Email,
            request.Website,
            request.LogoUrl,
            request.IsResponsableIva);

        _tenantConfigRepository.Update(config);
        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
