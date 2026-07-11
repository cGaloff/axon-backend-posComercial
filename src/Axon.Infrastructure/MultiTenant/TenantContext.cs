using Axon.Domain.Interfaces;

namespace Axon.Infrastructure.MultiTenant;

public class TenantContext : ITenantContext
{
    private string _schemaName = string.Empty;
    private string _tenantSlug = string.Empty;

    public string SchemaName
    {
        get => string.IsNullOrEmpty(_schemaName)
            ? throw new InvalidOperationException("Tenant context has not been set for this request.")
            : _schemaName;
        internal set => _schemaName = value;
    }

    public string TenantSlug
    {
        get => _tenantSlug;
        internal set => _tenantSlug = value;
    }

    // Público porque el middleware vive en Axon.API (otro assembly), pero solo
    // él debe invocarlo: el resto del código depende de ITenantContext, que no
    // expone este método, así que en la práctica solo quien recibe TenantContext
    // (el tipo concreto) puede reasignar el tenant de la petición.
    public void SetTenant(string slug, string schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new InvalidOperationException("Schema name cannot be empty.");
        }

        TenantSlug = slug;
        SchemaName = schemaName;
    }
}
