using Axon.Domain.Entities.Sales;
using Axon.Domain.Interfaces;
using TenantConfigEntity = Axon.Domain.Entities.TenantConfig;

namespace Axon.Application.Tests.TestSupport;

public class FakePdfService : IPdfService
{
    public byte[] GenerateSaleReceipt(Sale sale, TenantConfigEntity config) => Array.Empty<byte>();
}
