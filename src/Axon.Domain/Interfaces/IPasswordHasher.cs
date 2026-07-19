namespace Axon.Domain.Interfaces;

public interface IPasswordHasher
{
    bool Verify(string plainPassword, string hash);
    string Hash(string plainPassword);
}
