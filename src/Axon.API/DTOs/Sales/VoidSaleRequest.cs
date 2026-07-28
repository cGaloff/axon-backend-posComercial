namespace Axon.API.DTOs.Sales;

public record VoidSaleRequest(string Reason, string? SupervisorPin = null);
