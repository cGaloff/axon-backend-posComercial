using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities;

// Registro de auditoría inmutable: Create() es el ÚNICO punto de entrada y no
// expone ningún otro método público — no hay forma de alterar ni borrar un
// registro ya creado, ni siquiera para el Propietario (Matriz de Roles y
// Permisos v2, regla transversal A: "ni el mismo dueño debería poder tocar el
// log de auditoría sin que quede marca" — aquí directamente no hay forma de
// tocarlo en absoluto).
public class AuditLog
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    // "{Entidad}.{Acción}", ej. "Sale.Void", "InventoryMovement.Approve",
    // "Invoice.Export" — ver IAuditableRequest.AuditAction.
    public string Action { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AuditLog()
    {
    }

    public static AuditLog Create(Guid userId, string action, Guid? entityId)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainException("La acción del registro de auditoría es obligatoria.");
        }

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityId = entityId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
