namespace Axon.API.DTOs.CashRegister;

public record CloseCashSessionRequest(Guid ClosedBy, decimal CountedAmount, string? Notes, bool ForceClose = false);
