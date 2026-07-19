namespace Axon.API.DTOs.TenantConfig;

public class UpdateTenantConfigRequest
{
    public string BusinessName { get; set; } = default!;
    public string? Nit { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsResponsableIva { get; set; }
}