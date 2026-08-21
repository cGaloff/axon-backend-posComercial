namespace Axon.API.DTOs.Auth;

public record ForgotPasswordRequest(string Email, string TenantSlug);
