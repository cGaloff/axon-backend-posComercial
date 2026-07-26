using MediatR;

namespace Axon.Application.Inventory.Commands;

public record BulkCreateProductsCommand(List<CreateProductCommand> Products) : IRequest<BulkImportResult>;

// No hay UpdatedCount: un SKU que ya existe SIEMPRE se omite (nunca se
// actualiza un producto existente vía carga masiva) — decisión explícita para
// evitar sobrescribir precios/costos en producción por accidente con un
// archivo desactualizado.
public record BulkImportResult(int InsertedCount, int SkippedCount, List<string> Errors);
