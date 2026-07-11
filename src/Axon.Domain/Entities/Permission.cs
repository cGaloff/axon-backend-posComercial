using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities;

public class Permission
{
    public Guid Id { get; private set; }
    public string Module { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;

    public string Key => $"{Module}:{Action}";

    private Permission()
    {
    }

    public static Permission Create(string module, string action)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            throw new DomainException("Module is required.");
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainException("Action is required.");
        }

        return new Permission
        {
            Id = Guid.NewGuid(),
            Module = module,
            Action = action
        };
    }
}
