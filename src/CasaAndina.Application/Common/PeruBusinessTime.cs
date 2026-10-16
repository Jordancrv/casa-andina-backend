namespace CasaAndina.Application.Common;

/// <summary>Convierte fechas civiles del hotel (Perú) y marcas de tiempo UTC.</summary>
public static class PeruBusinessTime
{
    private static readonly TimeZoneInfo LimaTimeZone = ResolveLimaTimeZone();

    public static DateTime Today => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, LimaTimeZone).Date;

    public static DateTime UtcStartOfDay(DateTime peruDate)
    {
        var localDate = DateTime.SpecifyKind(peruDate.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localDate, LimaTimeZone);
    }

    public static DateTime FromUtc(DateTime utcTimestamp)
    {
        var utc = DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, LimaTimeZone);
    }

    private static TimeZoneInfo ResolveLimaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
        }
    }
}
