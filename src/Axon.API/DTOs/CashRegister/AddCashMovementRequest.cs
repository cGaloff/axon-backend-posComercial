using Axon.Domain.Entities.CashRegister;

namespace Axon.API.DTOs.CashRegister;

public record AddCashMovementRequest(CashMovementType Type, decimal Amount, string Description, Guid CreatedBy);
