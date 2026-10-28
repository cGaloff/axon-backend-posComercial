namespace Axon.Domain.Interfaces;

public interface ITenantContext
{
    string SchemaName { get; }
    string TenantSlug { get; }

    // Vacío si quien fijó el tenant no lo conocía (ej. webhooks).
    string BusinessName { get; }
}
