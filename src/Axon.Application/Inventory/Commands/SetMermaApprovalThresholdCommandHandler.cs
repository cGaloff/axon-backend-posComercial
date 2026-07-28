using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public class SetMermaApprovalThresholdCommandHandler : IRequestHandler<SetMermaApprovalThresholdCommand, MediatRUnit>
{
    private readonly ITenantConfigRepository _tenantConfigRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetMermaApprovalThresholdCommandHandler(ITenantConfigRepository tenantConfigRepository, IUnitOfWork unitOfWork)
    {
        _tenantConfigRepository = tenantConfigRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MediatRUnit> Handle(SetMermaApprovalThresholdCommand request, CancellationToken cancellationToken)
    {
        var config = await _tenantConfigRepository.GetAsync()
            ?? throw new DomainException("Configuración del tenant no encontrada");

        config.SetMermaApprovalThreshold(request.Threshold);

        _tenantConfigRepository.Update(config);
        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
    }
}
