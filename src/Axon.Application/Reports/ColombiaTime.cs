namespace Axon.Application.Reports;

internal static class ColombiaTime
{
    private static readonly TimeZoneInfo TimeZone = Resolve();

    public static DateTime ToLocal(DateTime utcDateTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, TimeZone);
    }

    private static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback para Windows sin datos IANA (el ID de Windows es distinto al de Linux/Mac).
            return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
        }
    }
}
