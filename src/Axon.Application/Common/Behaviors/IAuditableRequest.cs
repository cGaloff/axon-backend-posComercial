namespace Axon.Application.Common.Behaviors;

// Marcador opt-in: solo los comandos/queries que representan una acción
// sensible (Matriz de Roles y Permisos v2) implementan esta interfaz. No se
// audita CADA request que pasa por MediatR a propósito — eso generaría
// demasiado ruido y ocultaría las acciones que sí importan revisar.
public interface IAuditableRequest
{
    // "{Entidad}.{Acción}", ej. "Sale.Void", "Invoice.Export".
    string AuditAction { get; }

    // Id de la entidad afectada, si aplica y ya se conoce a partir del propio
    // request (null en creaciones, donde el Id todavía no existe al momento
    // de construir el comando).
    Guid? AuditEntityId { get; }
}
