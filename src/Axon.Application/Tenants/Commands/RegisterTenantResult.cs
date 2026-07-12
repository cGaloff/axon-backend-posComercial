namespace Axon.Application.Tenants.Commands;

public record RegisterTenantResult(
    Guid TenantId,
    string SchemaName,
    string Slug,
    string BusinessName);
