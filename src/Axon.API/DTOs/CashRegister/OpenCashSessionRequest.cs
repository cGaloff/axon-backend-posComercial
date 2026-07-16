namespace Axon.API.DTOs.CashRegister;

public record OpenCashSessionRequest(Guid CashRegisterId, decimal InitialAmount);
