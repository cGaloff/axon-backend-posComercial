using Axon.Application.Interfaces;
using Axon.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Sales;

// Resuelve el nombre del cajero y de la caja para el PDF del recibo/factura.
// Sale solo guarda CreatedBy/CashRegisterId como Guid (no se toca el dominio
// para agregar esos nombres); este resolver vive en Application y arma el
// view-model que consume IPdfService.
public record SaleReceiptContext(string CashierName, string CashRegisterName)
{
    public static async Task<SaleReceiptContext> ResolveAsync(
        IApplicationDbContext dbContext, Sale sale, CancellationToken cancellationToken)
    {
        var cashierName = await dbContext.Users
            .Where(u => u.Id == sale.CreatedBy)
            .Select(u => u.FullName)
            .SingleOrDefaultAsync(cancellationToken) ?? "(usuario no encontrado)";

        var cashRegisterName = await dbContext.CashRegisters
            .Where(r => r.Id == sale.CashRegisterId)
            .Select(r => r.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "(caja no encontrada)";

        return new SaleReceiptContext(cashierName, cashRegisterName);
    }
}
