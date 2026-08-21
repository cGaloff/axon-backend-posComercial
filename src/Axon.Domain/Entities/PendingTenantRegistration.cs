using System.Security.Cryptography;
using System.Text;

namespace Axon.Domain.Entities;

public class PendingTenantRegistration
{
    public const int MaxCodeAttempts = 5;

    public Guid Id { get; private set; }
    public string BusinessName { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string OwnerEmail { get; private set; } = string.Empty;
    public string OwnerPasswordHash { get; private set; } = string.Empty;
    public string Plan { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public int FailedAttempts { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }

    public bool IsActive => ConsumedAt is null && ExpiresAt > DateTime.UtcNow;

    private PendingTenantRegistration()
    {
    }

    public static PendingTenantRegistration Create(
        string businessName,
        string slug,
        string ownerEmail,
        string ownerPasswordHash,
        string plan,
        string codeHash,
        DateTime expiresAt)
    {
        return new PendingTenantRegistration
        {
            Id = Guid.NewGuid(),
            BusinessName = businessName,
            Slug = slug,
            OwnerEmail = ownerEmail,
            OwnerPasswordHash = ownerPasswordHash,
            Plan = plan,
            CodeHash = codeHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Mismo criterio que User.RegisterFailedLoginAttempt: al llegar al máximo
    // de intentos, la solicitud queda consumida (inutilizable) sin esperar a
    // que expire por tiempo — un código de 6 dígitos es adivinable si no se
    // corta el intento de fuerza bruta ahí mismo.
    public void RegisterFailedAttempt()
    {
        FailedAttempts++;

        if (FailedAttempts >= MaxCodeAttempts)
        {
            ConsumedAt = DateTime.UtcNow;
        }
    }

    public void MarkConsumed()
    {
        ConsumedAt = DateTime.UtcNow;
    }

    // Mismo criterio que RefreshToken.HashToken/PasswordResetToken.HashToken:
    // el código crudo nunca se persiste, solo este hash.
    public static string HashVerificationCode(string rawCode)
    {
        var bytes = Encoding.UTF8.GetBytes(rawCode);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
