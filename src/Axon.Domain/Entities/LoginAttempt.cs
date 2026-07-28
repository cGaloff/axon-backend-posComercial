using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities;

// Historial de intentos de login (Matriz de Roles y Permisos v2, "seguridad
// técnica") — inmutable, un registro por intento (exitoso o fallido). UserId
// es null si el email no correspondía a ningún usuario real (no hay a quién
// atribuirlo, pero igual queda constancia del intento).
public class LoginAttempt
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public Guid? UserId { get; private set; }
    public bool Success { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private LoginAttempt()
    {
    }

    public static LoginAttempt Create(string email, Guid? userId, bool success, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("El email del intento de login es obligatorio.");
        }

        return new LoginAttempt
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserId = userId,
            Success = success,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };
    }
}
