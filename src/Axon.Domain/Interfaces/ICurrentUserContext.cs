namespace Axon.Domain.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }
}
