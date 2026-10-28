namespace Axon.Domain.Entities;

// Ciclos de facturación del plan único. Los precios viven en el
// microservicio de pagos; acá solo importa cuántos días suma cada ciclo
// cuando el webhook de pago no los manda explícitos.
public static class BillingCycles
{
    public const string Mensual = "mensual";
    public const string Trimestral = "trimestral";
    public const string Semestral = "semestral";
    public const string Anual = "anual";

    private static readonly Dictionary<string, int> ExtensionDaysByCycle = new(StringComparer.OrdinalIgnoreCase)
    {
        [Mensual] = 30,
        [Trimestral] = 90,
        [Semestral] = 180,
        [Anual] = 365
    };

    public static IReadOnlyCollection<string> All => ExtensionDaysByCycle.Keys;

    public static bool IsValid(string? cycle)
    {
        return cycle is not null && ExtensionDaysByCycle.ContainsKey(cycle);
    }

    public static int ExtensionDaysFor(string cycle)
    {
        return ExtensionDaysByCycle[cycle];
    }
}
