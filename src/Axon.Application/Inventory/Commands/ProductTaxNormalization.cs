using Axon.Application.Interfaces;
using Axon.Domain.Entities.Taxes;
using Axon.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Inventory.Commands;

// Compartido por CreateProductCommandHandler y UpdateProductCommandHandler:
// valida que cada TaxTypeId exista/esté activo y, si es IVA, que el
// porcentaje sea uno de los fijos por norma tributaria colombiana (19%, 10%,
// 5% o exento/0% — ver TaxType.AllowedIvaPercentages). El resto de los
// impuestos del catálogo fijo admite cualquier porcentaje, igual que antes.
public static class ProductTaxNormalization
{
    public static async Task<List<(Guid TaxTypeId, decimal Percentage)>> NormalizeAsync(
        IApplicationDbContext dbContext,
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
            var taxType = await dbContext.TaxTypes.SingleOrDefaultAsync(
                t => t.Id == tax.TaxTypeId && t.IsActive, cancellationToken);

            if (taxType is null)
            {
                throw new DomainException($"El tipo de impuesto '{tax.TaxTypeId}' no existe o está inactivo");
            }

            if (!TaxType.IsValidPercentageFor(taxType.Code, tax.Percentage))
            {
                throw new DomainException(
                    $"El IVA solo admite las tarifas 19%, 10%, 5% o exento (0%). Se recibió {tax.Percentage}%.");
            }

            normalized.Add((tax.TaxTypeId, tax.Percentage));
        }

        return normalized;
    }
}
