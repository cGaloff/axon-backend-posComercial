namespace Axon.API.DTOs.Sales;

public record PaymentConfirmedWebhookRequest(Guid SaleId, string TenantSlug, string ExternalTransactionId);
