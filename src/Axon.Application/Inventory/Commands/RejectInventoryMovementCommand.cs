using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public record RejectInventoryMovementCommand(Guid MovementId, string Reason) : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "InventoryMovement.Reject";
    public Guid? AuditEntityId => MovementId;
}
