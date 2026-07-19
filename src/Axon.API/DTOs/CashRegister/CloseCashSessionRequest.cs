namespace Axon.API.DTOs.CashRegister;

public record CloseCashSessionRequest(decimal CountedAmount, string? Notes, bool ForceClose = false);
