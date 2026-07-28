namespace Axon.API.DTOs.CashRegister;

public record OpenCashSessionRequest(Guid CashRegisterId, Guid CashierId, decimal InitialAmount);
