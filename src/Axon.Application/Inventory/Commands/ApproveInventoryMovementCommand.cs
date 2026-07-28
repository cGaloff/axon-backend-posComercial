using Axon.Application.Common.Behaviors;
using MediatR;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public record ApproveInventoryMovementCommand(Guid MovementId) : IRequest<MediatRUnit>, IAuditableRequest
{
    public string AuditAction => "InventoryMovement.Approve";
    public Guid? AuditEntityId => MovementId;
}
