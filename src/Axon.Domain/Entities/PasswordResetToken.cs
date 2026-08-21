using System.Security.Cryptography;
using System.Text;

namespace Axon.Domain.Entities;

public class PasswordResetToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UsedAt { get; private set; }

    public bool IsActive => UsedAt is null && ExpiresAt > DateTime.UtcNow;

    private PasswordResetToken()
    {
    }

    public static PasswordResetToken Create(Guid userId, string tokenHash, DateTime expiresAt)
    {
        return new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkUsed()
    {
        UsedAt = DateTime.UtcNow;
    }

    // Mismo criterio que RefreshToken.HashToken: el valor crudo del token nunca
    // se persiste, solo este hash SHA-256, para poder buscarlo por igualdad
    // directa en la columna indexada a partir del token que manda el cliente.
    public static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
