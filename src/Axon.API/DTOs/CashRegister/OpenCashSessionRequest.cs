namespace Axon.API.DTOs.CashRegister;

public record OpenCashSessionRequest(Guid CashRegisterId, Guid OpenedBy, decimal InitialAmount);
