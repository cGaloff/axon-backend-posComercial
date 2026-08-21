namespace Axon.API.DTOs.Auth;

public record ResetPasswordRequest(string Token, string NewPassword, string TenantSlug);
