namespace Axon.Domain.Interfaces;

public interface ITenantContext
{
    string SchemaName { get; }
    string TenantSlug { get; }
}
