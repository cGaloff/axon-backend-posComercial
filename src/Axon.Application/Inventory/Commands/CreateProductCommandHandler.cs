using System.Text.Json;
using Axon.Application.Interfaces;
using Axon.Domain.Entities.Inventory;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Commands;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Solo bloquea SKUs de productos ACTIVOS: Deactivate() es un soft-delete
        // (el producto sigue en la tabla con IsActive = false), así que un SKU
        // "eliminado" debe quedar libre para reutilizarse en un producto nuevo.
        var skuInUse = await _dbContext.Products.AnyAsync(p => p.Sku == request.Sku && p.IsActive, cancellationToken);

        if (skuInUse)
        {
            throw new DomainException($"Ya existe un producto con el SKU '{request.Sku}'");
        }

        var product = Product.Create(
            request.Sku,
            request.Name,
            request.Price,
            request.Cost,
            request.MinStock,
            request.CategoryId,
            request.UnitId);

        // Product.Create() no acepta description (no está en su firma); UpdateDetails()
        // es el único método que la fija, así que se llama inmediatamente después con
        // los mismos valores para completar la creación con la descripción incluida.
        product.UpdateDetails(
            request.Name,
            request.Description,
            request.Price,
            request.Cost,
            request.MinStock,
            request.CategoryId,
            request.UnitId);

        if (request.Attributes is { Count: > 0 })
        {
            var normalizedAttributes = await NormalizeAttributesAsync(request.Attributes, request.CategoryId, cancellationToken);
            product.SetAttributes(normalizedAttributes);
        }

        // Taxes representa el estado completo deseado (a diferencia de Attributes,
        // que solo se toca si viene con datos): una lista nula o vacía deja el
        // producto sin ningún impuesto configurado, de forma explícita.
        var normalizedTaxes = await NormalizeTaxesAsync(request.Taxes, cancellationToken);
        product.SetTaxes(normalizedTaxes);

        _dbContext.Products.Add(product);
        await _unitOfWork.CommitAsync(cancellationToken);

        return product.Id;
    }

    private async Task<Dictionary<string, JsonElement>> NormalizeAttributesAsync(
        Dictionary<string, string> attributes,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var normalized = new Dictionary<string, JsonElement>();

        foreach (var (rawKey, value) in attributes)
        {
            var key = rawKey.Trim().ToLowerInvariant().Replace(' ', '_');

            var definitionExists = await _dbContext.AttributeDefinitions.AnyAsync(
                d => d.Key == key && (d.CategoryId == null || d.CategoryId == categoryId),
                cancellationToken);

            if (!definitionExists)
            {
                throw new DomainException($"El atributo '{key}' no está definido para esta categoría");
            }

            normalized[key] = JsonSerializer.SerializeToElement(value);
        }

        return normalized;
    }

    private async Task<List<(Guid TaxTypeId, decimal Percentage)>> NormalizeTaxesAsync(
        List<ProductTaxRequest>? taxes,
        CancellationToken cancellationToken)
    {
        var normalized = new List<(Guid TaxTypeId, decimal Percentage)>();

        if (taxes is null)
        {
            return normalized;
        }

        foreach (var tax in taxes)
        {
            var taxTypeExists = await _dbContext.TaxTypes.AnyAsync(
                t => t.Id == tax.TaxTypeId && t.IsActive, cancellationToken);

            if (!taxTypeExists)
            {
                throw new DomainException($"El tipo de impuesto '{tax.TaxTypeId}' no existe o está inactivo");
            }

            normalized.Add((tax.TaxTypeId, tax.Percentage));
        }

        return normalized;
    }
}
