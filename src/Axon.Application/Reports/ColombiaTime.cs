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
            try
            {
                // Fallback para Windows sin datos IANA (el ID de Windows es distinto al de Linux/Mac).
                return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                // Ultimo recurso: no depende de NINGUN dato de zona horaria del sistema
                // operativo. Necesario porque contenedores Linux sin tzdata/ICU completos
                // (comun en imagenes minimalistas) hacen fallar los dos intentos de arriba
                // a la vez, tirando abajo la inicializacion estatica de esta clase entera.
                // Colombia no tiene horario de verano, asi que UTC-5 fijo es exacto siempre.
                return TimeZoneInfo.CreateCustomTimeZone("Colombia-Fixed-UTC-5", TimeSpan.FromHours(-5), "Colombia (fijo)", "Colombia (fijo)");
            }
        }
    }
}
