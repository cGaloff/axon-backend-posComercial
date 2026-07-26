using Axon.Domain.Entities;
using Axon.Domain.Entities.Sales;

namespace Axon.Domain.Interfaces;

public interface IPdfService
{
    // cashierName es un dato resuelto por el caller (Sale solo guarda CreatedBy
    // como Guid) — se pasa ya resuelto para no acoplar el generador de PDF a
    // IApplicationDbContext.
    byte[] GenerateSaleReceipt(Sale sale, TenantConfig config, string cashierName);
}
