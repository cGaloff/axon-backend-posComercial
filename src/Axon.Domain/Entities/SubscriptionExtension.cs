using Axon.Domain.Exceptions;

namespace Axon.Domain.Entities;

// Historial de renovaciones de un tenant (base maestra). Sirve para dos cosas:
// - Idempotencia del webhook: la pasarela reintenta, y sin esto un mismo pago
//   sumaría los días dos veces. TransactionReference es único.
// - Mostrar el último pago (monto y fecha) en GET /api/subscription/status.
public class SubscriptionExtension
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string? BillingCycle { get; private set; }
    public int ExtensionDays { get; private set; }
    public decimal Amount { get; private set; }
    public string? TransactionReference { get; private set; }
    public DateTime? PreviousExpiresAt { get; private set; }
    public DateTime NewExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SubscriptionExtension()
    {
    }

    public static SubscriptionExtension Create(
        Guid tenantId,
        string? billingCycle,
        int extensionDays,
        decimal amount,
        string? transactionReference,
        DateTime? previousExpiresAt,
        DateTime newExpiresAt)
    {
        if (amount < 0)
        {
            throw new DomainException("El monto pagado no puede ser negativo.");
        }

        return new SubscriptionExtension
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BillingCycle = billingCycle,
            ExtensionDays = extensionDays,
            Amount = amount,
            TransactionReference = string.IsNullOrWhiteSpace(transactionReference) ? null : transactionReference.Trim(),
            PreviousExpiresAt = previousExpiresAt,
            NewExpiresAt = newExpiresAt,
            CreatedAt = DateTime.UtcNow
        };
    }
}
