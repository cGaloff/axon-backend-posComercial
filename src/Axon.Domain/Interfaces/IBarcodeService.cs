namespace Axon.Domain.Interfaces;

public interface IBarcodeService
{
    // content debe ser el texto a codificar (en este proyecto, Product.Sku).
    // Devuelve un PNG, igual patrón que IPdfService: se genera bajo demanda,
    // nunca se persiste como blob en la base de datos.
    byte[] GenerateBarcode(string content);
}
