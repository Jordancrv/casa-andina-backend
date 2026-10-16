using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CasaAndina.Infrastructure.Services;

public static class JwtTokenSettings
{
    public const double DefaultExpirationHours = 4;
    public const double MaximumExpirationHours = 8;

    public static double GetExpirationHours(IConfiguration configuration)
    {
        var configuredValue = configuration["Jwt:ExpirationHours"];
        if (configuredValue is null)
            return DefaultExpirationHours;

        if (!double.TryParse(configuredValue, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var hours)
            || !double.IsFinite(hours)
            || hours < 1
            || hours > MaximumExpirationHours)
        {
            throw new InvalidOperationException(
                $"Jwt:ExpirationHours debe ser un número entre 1 y {MaximumExpirationHours}.");
        }

        return hours;
    }
}
