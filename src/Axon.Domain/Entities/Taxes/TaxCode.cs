namespace Axon.Domain.Entities.Taxes;

// Catálogo cerrado de impuestos colombianos aplicables a un producto. Fijo a
// propósito: ya no existe un comando para crear tipos de impuesto
// personalizados (ver TaxType) — el tenant solo puede activar/desactivar
// cuáles de estos 8 le aplican a su negocio.
public enum TaxCode
{
    Iva,
    Gmf,
    Inc,
    IncPl,
    Ica,
    Incombustible,
    Incarbono,
    Ibua
}
