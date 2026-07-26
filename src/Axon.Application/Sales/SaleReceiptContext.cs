using Axon.Application.Interfaces;
using Axon.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Sales;

// Resuelve el nombre del cajero para el PDF del recibo/factura. Sale solo
// guarda CreatedBy como Guid (no se toca el dominio para agregar el nombre);
// este resolver vive en Application y arma el dato que consume IPdfService.
public static class SaleReceiptContext
{
    public static async Task<string> ResolveCashierNameAsync(
        IApplicationDbContext dbContext, Sale sale, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Where(u => u.Id == sale.CreatedBy)
            .Select(u => u.FullName)
            .SingleOrDefaultAsync(cancellationToken) ?? "(usuario no encontrado)";
    }
}
