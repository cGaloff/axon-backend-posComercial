namespace Axon.Application.Interfaces;

public interface ITenantSchemaInitializer
{
    Task InitializeSchemaAsync(string schemaName);
    Task DropSchemaAsync(string schemaName);
}
