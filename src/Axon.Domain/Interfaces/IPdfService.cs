using Axon.Domain.Entities;
using Axon.Domain.Entities.Sales;

namespace Axon.Domain.Interfaces;

public interface IPdfService
{
    // cashierName/cashRegisterName son datos resueltos por el caller (Sale solo
    // guarda CreatedBy/CashRegisterId como Guid) — se pasan ya resueltos para no
    // acoplar el generador de PDF a IApplicationDbContext.
    byte[] GenerateSaleReceipt(Sale sale, TenantConfig config, string cashierName, string cashRegisterName);
}
