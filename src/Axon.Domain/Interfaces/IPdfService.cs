using Axon.Domain.Entities.Sales;

namespace Axon.Domain.Interfaces;

public interface IPdfService
{
    byte[] GenerateSaleReceipt(Sale sale, string businessName);
}
