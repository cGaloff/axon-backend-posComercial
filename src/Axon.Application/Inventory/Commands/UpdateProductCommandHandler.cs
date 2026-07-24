using System.Text.Json;
using Axon.Application.Interfaces;
using Axon.Domain.Exceptions;
using Axon.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MediatRUnit = MediatR.Unit;

namespace Axon.Application.Inventory.Commands;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, MediatRUnit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductCommandHandler(IApplicationDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<MediatRUnit> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
        {
            throw new DomainException("Producto no encontrado");
        }

        // El SKU es inmutable: UpdateProductCommand no lo incluye ni lo modifica.
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

        // Taxes representa el estado completo deseado: una lista nula o vacía deja
        // el producto sin ningún impuesto configurado, de forma explícita.
        var normalizedTaxes = await NormalizeTaxesAsync(request.Taxes, cancellationToken);
        product.SetTaxes(normalizedTaxes);

        await _unitOfWork.CommitAsync(cancellationToken);

        return MediatRUnit.Value;
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
