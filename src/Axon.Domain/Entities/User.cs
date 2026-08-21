using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;

    // PIN corto opcional (hash independiente de PasswordHash) para autorizar,
    // sin cerrar la sesión del Cajero, acciones que requieren un rol superior
    // (ej. anular/devolver una venta) — Matriz de Roles y Permisos v2.
    public string? PinHash { get; private set; }

    public Guid RoleId { get; private set; }
    public Role? Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Bloqueo por intentos fallidos de login (Matriz de Roles y Permisos v2,
    // "seguridad técnica"): tras MaxFailedLoginAttempts seguidos, la cuenta
    // queda bloqueada por LockoutDuration, sin importar que la contraseña
    // correcta se use después — hay que esperar a que se cumpla el plazo.
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedUntil { get; private set; }

    // Última vez que este usuario inició sesión o renovó su sesión — usado
    // para el cierre de sesión por inactividad (ver RefreshTokenCommandHandler).
    public DateTime? LastActivityAt { get; private set; }

    public bool IsLockedOut => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;

    private User()
    {
    }

    public static User Create(string fullName, string email, string passwordHash, Guid roleId)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Email is required.");
        }

        if (!email.Contains('@') || !email.Contains('.'))
        {
            throw new DomainException("Email is invalid.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("Full name is required.");
        }

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            Email = email,
            PasswordHash = passwordHash,
            RoleId = roleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }

    public void Update(string fullName, Guid roleId)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("Full name is required.");
        }

        FullName = fullName;
        RoleId = roleId;
    }

    public void ChangePassword(string newHash)
    {
        PasswordHash = newHash;
    }

    // Reset por email: a diferencia de ChangePassword (cambio de admin/propio,
    // ya autenticado), acá probar la identidad por email es una señal fuerte,
    // así que además de la contraseña se limpia cualquier bloqueo por intentos
    // fallidos previo.
    public void ResetPassword(string newHash)
    {
        PasswordHash = newHash;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    public void SetPin(string pinHash)
    {
        if (string.IsNullOrWhiteSpace(pinHash))
        {
            throw new DomainException("El PIN es obligatorio.");
        }

        PinHash = pinHash;
    }

    // Contraseña incorrecta: suma un intento fallido; al llegar al máximo,
    // bloquea la cuenta por LockoutDuration a partir de AHORA (cada intento
    // fallido adicional mientras está bloqueada extiende el bloqueo, en vez de
    // dejarlo vencer mientras alguien sigue intentando).
    public void RegisterFailedLoginAttempt()
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
        }
    }

    // Login exitoso: limpia el contador y cualquier bloqueo previo, y marca
    // actividad reciente.
    public void RegisterSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockedUntil = null;
        LastActivityAt = DateTime.UtcNow;
    }

    public void RecordActivity()
    {
        LastActivityAt = DateTime.UtcNow;
    }
}
