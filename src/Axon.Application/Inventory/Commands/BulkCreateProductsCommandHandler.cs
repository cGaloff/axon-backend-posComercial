using System.Text.Json;
using Axon.Application.Interfaces;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Commands;

// Carga masiva de productos (importación desde Excel/CSV en el frontend).
// Todas las validaciones que normalmente irían una-por-una (SKU duplicado,
// categoría/unidad/impuestos existentes) se resuelven aquí con una sola
// consulta por catálogo, cargando el resultado en memoria (HashSet/Dictionary)
// antes de recorrer las filas: con cientos o miles de productos en un solo
// request, una consulta por fila haría el endpoint inutilizable (N+1).
public class BulkCreateProductsCommandHandler : IRequestHandler<BulkCreateProductsCommand, BulkImportResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateProductCommand> _productValidator;

    public BulkCreateProductsCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        IValidator<CreateProductCommand> productValidator)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _productValidator = productValidator;
    }

    public async Task<BulkImportResult> Handle(BulkCreateProductsCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var newProducts = new List<Product>();

        // --- Carga en lote de todo lo necesario para validar, en 4 consultas
        // totales sin importar cuántas filas traiga el archivo. ---

        var inputSkus = request.Products.Select(p => p.Sku).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existingSkus = await _dbContext.Products
            .Where(p => p.IsActive && inputSkus.Contains(p.Sku))
            .Select(p => p.Sku)
            .ToListAsync(cancellationToken);
        var existingSkuSet = new HashSet<string>(existingSkus, StringComparer.OrdinalIgnoreCase);

        var categoryIds = request.Products.Select(p => p.CategoryId).Distinct().ToList();
        var validCategoryIds = await _dbContext.Categories
            .Where(c => c.IsActive && categoryIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
        var validCategorySet = new HashSet<Guid>(validCategoryIds);

        var unitIds = request.Products.Select(p => p.UnitId).Distinct().ToList();
        var validUnitIds = await _dbContext.Units
            .Where(u => unitIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        var validUnitSet = new HashSet<Guid>(validUnitIds);

        var taxTypeIds = request.Products
            .SelectMany(p => p.Taxes ?? new List<ProductTaxRequest>())
            .Select(t => t.TaxTypeId)
            .Distinct()
            .ToList();
        var activeTaxTypeIds = await _dbContext.TaxTypes
            .Where(t => t.IsActive && taxTypeIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        var activeTaxTypeSet = new HashSet<Guid>(activeTaxTypeIds);

        // El catálogo de definiciones de atributos es chico (decenas, no miles):
        // se carga completo una sola vez y se filtra en memoria por fila, en vez
        // de una consulta por cada atributo de cada producto.
        var attributeDefinitions = await _dbContext.AttributeDefinitions.ToListAsync(cancellationToken);

        // SKUs repetidos DENTRO del mismo archivo: si no se detecta aquí, dos
        // filas nuevas con el mismo SKU pasarían la validación contra existingSkuSet
        // (ninguna existe todavía en la BD) y ambas llegarían a AddRangeAsync,
        // reventando la restricción única de la BD y abortando el batch completo.
        var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in request.Products)
        {
            var validationResult = await _productValidator.ValidateAsync(row, cancellationToken);
            if (!validationResult.IsValid)
            {
                errors.Add($"SKU '{row.Sku}': {string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage))}");
                continue;
            }

            if (existingSkuSet.Contains(row.Sku) || !seenSkus.Add(row.Sku))
            {
                errors.Add($"SKU '{row.Sku}' ya existe y fue omitido.");
                continue;
            }

            if (!validCategorySet.Contains(row.CategoryId))
            {
                errors.Add($"SKU '{row.Sku}': la categoría no existe o está inactiva.");
                continue;
            }

            if (!validUnitSet.Contains(row.UnitId))
            {
                errors.Add($"SKU '{row.Sku}': la unidad no existe.");
                continue;
            }

            var invalidTax = row.Taxes?.FirstOrDefault(t => !activeTaxTypeSet.Contains(t.TaxTypeId));
            if (invalidTax is not null)
            {
                errors.Add($"SKU '{row.Sku}': el impuesto '{invalidTax.TaxTypeId}' no existe o está inactivo.");
                continue;
            }

            if (!TryNormalizeAttributes(row, attributeDefinitions, out var normalizedAttributes, out var attributeError))
            {
                errors.Add($"SKU '{row.Sku}': {attributeError}");
                continue;
            }

            try
            {
                var product = Product.Create(row.Sku, row.Name, row.Price, row.Cost, row.MinStock, row.CategoryId, row.UnitId);
                product.UpdateDetails(row.Name, row.Description, row.Price, row.Cost, row.MinStock, row.CategoryId, row.UnitId);

                if (normalizedAttributes.Count > 0)
                {
                    product.SetAttributes(normalizedAttributes);
                }

                product.SetTaxes(row.Taxes?.Select(t => (t.TaxTypeId, t.Percentage)) ?? Enumerable.Empty<(Guid, decimal)>());

                newProducts.Add(product);
            }
            catch (DomainException ex)
            {
                // Defensa adicional: cualquier invariante de dominio no cubierta
                // por el validador (o una nueva regla agregada a futuro a
                // Product.Create) se omite fila-por-fila en vez de abortar el
                // resto del batch ya validado.
                errors.Add($"SKU '{row.Sku}': {ex.Message}");
            }
        }

        if (newProducts.Count > 0)
        {
            await _dbContext.Products.AddRangeAsync(newProducts, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }

        var skippedCount = request.Products.Count - newProducts.Count;

        return new BulkImportResult(newProducts.Count, skippedCount, errors);
    }

    private static bool TryNormalizeAttributes(
        CreateProductCommand row,
        List<AttributeDefinition> definitions,
        out Dictionary<string, JsonElement> normalized,
        out string error)
    {
        normalized = new Dictionary<string, JsonElement>();
        error = string.Empty;

        if (row.Attributes is not { Count: > 0 })
        {
            return true;
        }

        foreach (var (rawKey, value) in row.Attributes)
        {
            var key = rawKey.Trim().ToLowerInvariant().Replace(' ', '_');

            var definitionExists = definitions.Any(d => d.Key == key && (d.CategoryId == null || d.CategoryId == row.CategoryId));

            if (!definitionExists)
            {
                error = $"el atributo '{key}' no está definido para esta categoría.";
                return false;
            }

            normalized[key] = JsonSerializer.SerializeToElement(value);
        }

        return true;
    }
}
