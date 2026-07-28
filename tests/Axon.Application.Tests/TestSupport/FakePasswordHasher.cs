using Axon.Domain.Interfaces;

namespace Axon.Application.Tests.TestSupport;

// Hash trivial y reversible-por-comparación (no cifrado real) — solo para que
// Hash()/Verify() sean consistentes entre sí en las pruebas, sin el costo de
// un hasher real (BCrypt) que no aporta nada en un test unitario.
public class FakePasswordHasher : IPasswordHasher
{
    private const string Prefix = "hashed:";

    public string Hash(string plainPassword) => Prefix + plainPassword;

    public bool Verify(string plainPassword, string hash) => hash == Prefix + plainPassword;
}
