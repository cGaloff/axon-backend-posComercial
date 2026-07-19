using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities;

public class Role
{
    private readonly List<Permission> _permissions = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public string Description { get; private set; } = string.Empty;

    public IReadOnlyList<Permission> Permissions => _permissions;

    private Role()
    {
    }

    public static Role Create(string name, string description, bool isSystem)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Name is required.");
        }

        return new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            IsSystem = isSystem
        };
    }

    public void AddPermission(Permission permission)
    {
        _permissions.Add(permission);
    }
}
