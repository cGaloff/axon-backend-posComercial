using Axon.Application.Interfaces;

namespace Axon.Application.Tests.TestSupport;

public class FakeTenantCacheInvalidator : ITenantCacheInvalidator
{
    public List<string> InvalidatedSlugs { get; } = new();

    public void Invalidate(string slug)
    {
        InvalidatedSlugs.Add(slug);
    }
}
