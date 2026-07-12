namespace Axon.API.DTOs.Tenants;

public record RegisterTenantRequest(
    string BusinessName,
    string Slug,
    string OwnerEmail,
    string OwnerPassword,
    string Plan);
