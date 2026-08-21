namespace Axon.API.DTOs.Tenants;

public record ConfirmTenantRegistrationRequest(Guid PendingRegistrationId, string Code);
