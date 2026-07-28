using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public record SetMermaApprovalThresholdCommand(decimal Threshold) : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "TenantConfig.SetMermaApprovalThreshold";
    public Guid? AuditEntityId => null;
}
