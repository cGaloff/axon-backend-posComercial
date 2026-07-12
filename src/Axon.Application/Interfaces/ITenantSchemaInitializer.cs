namespace Axon.Application.Interfaces;

public interface ITenantSchemaInitializer
{
    Task InitializeSchemaAsync(string schemaName);
}
