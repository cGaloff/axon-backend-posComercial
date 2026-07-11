namespace Axon.API.DTOs.Auth;

public record LoginRequest(string Email, string Password, string TenantSlug);
