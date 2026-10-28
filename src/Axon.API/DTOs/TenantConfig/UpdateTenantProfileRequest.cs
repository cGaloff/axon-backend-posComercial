namespace Axon.API.DTOs.TenantConfig;

public class UpdateTenantProfileRequest
{
    public string BusinessName { get; set; } = default!;
    public string? LegalName { get; set; }
    public string? Nit { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    // Sin default a propósito: cambia IsResponsableIva (impuestos en factura),
    // así que omitirlo es un 400 y no un cambio de régimen silencioso.
    public string TaxRegime { get; set; } = default!;
}
