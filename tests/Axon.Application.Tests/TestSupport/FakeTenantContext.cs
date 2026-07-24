using Axon.Domain.Interfaces;

namespace Axon.Application.Tests.TestSupport;

public class FakeTenantContext : ITenantContext
{
    public string SchemaName => "tenant_test";

    public string TenantSlug => "test-tenant";
}
