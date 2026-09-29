namespace Axon.Application.Interfaces;

public interface ITenantCacheInvalidator
{
    void Invalidate(string slug);
}
