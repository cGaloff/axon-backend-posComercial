using Axon.Domain.Interfaces;

namespace Axon.Infrastructure.Security;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string plainPassword)
    {
        return BCrypt.Net.BCrypt.HashPassword(plainPassword);
    }

    public bool Verify(string plainPassword, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(plainPassword, hash);
    }
}
