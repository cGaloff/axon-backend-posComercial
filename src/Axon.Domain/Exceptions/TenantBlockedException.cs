namespace Axon.Domain.Exceptions;

// El negocio no puede operar (suspendido o vencido). Se responde 403 con
// un "code" para que el cliente lo distinga de un 403 por falta de permiso:
// solo este caso manda al usuario a la pantalla de cuenta bloqueada.
public class TenantBlockedException : DomainException
{
    public const string SuspendedCode = "TENANT_SUSPENDED";
    public const string SubscriptionExpiredCode = "SUBSCRIPTION_EXPIRED";

    public string Code { get; }

    public TenantBlockedException(string message, string code)
        : base(message)
    {
        Code = code;
    }

    public static TenantBlockedException Suspended() => new("Tenant suspendido.", SuspendedCode);

    public static TenantBlockedException SubscriptionExpired() => new("Suscripción vencida.", SubscriptionExpiredCode);
}
