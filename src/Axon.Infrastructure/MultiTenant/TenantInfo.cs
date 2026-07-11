namespace Axon.Infrastructure.MultiTenant;

public record TenantInfo(string Slug, string SchemaName, bool IsActive);
