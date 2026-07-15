using Axon.Domain.Entities.CashRegister;

namespace Axon.Domain.Interfaces;

public interface ICashSessionRepository
{
    Task<CashSession?> GetActiveSessionAsync(Guid cashRegisterId);
    Task<CashSession?> GetByIdAsync(Guid id);
    Task AddAsync(CashSession session);
    void Update(CashSession session);
}
