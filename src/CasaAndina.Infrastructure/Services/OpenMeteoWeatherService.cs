using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CasaAndina.Application.Chatbot.DTOs;
using CasaAndina.Application.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CasaAndina.Infrastructure.Services;

public interface ITravelWeatherService
{
    Task<ClimaViajeDto> ConsultarAsync(string ciudad, DateTime entrada, DateTime salida, CancellationToken cancellationToken);
}

public sealed class OpenMeteoWeatherService(HttpClient http, IMemoryCache cache,
    ILogger<OpenMeteoWeatherService> logger) : ITravelWeatherService
{
    public async Task<ClimaViajeDto> ConsultarAsync(string ciudad, DateTime entrada, DateTime salida, CancellationToken cancellationToken)
    {
        var hoy = PeruBusinessTime.Today;
        if (entrada.Date > hoy.AddDays(15))
            return SinDatos("fuera_de_rango", "El pronóstico aún no cubre estas fechas. Vuelve a consultar cuando falten menos de 16 días.");

        var clave = $"weather:{ciudad}:{hoy:yyyy-MM-dd}:{entrada:yyyy-MM-dd}:{salida:yyyy-MM-dd}";
        if (cache.TryGetValue<ClimaViajeDto>(clave, out var existente) && existente is not null) return existente;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var nombre = ciudad.Split('(')[0].Trim();
            var geoKey = $"weather-city:{Normalizar(nombre)}";
            if (!cache.TryGetValue<Localidad>(geoKey, out var localidad) || localidad is null)
            {
                using var geo = await http.GetFromJsonAsync<JsonDocument>(
                    $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(nombre)}&count=10&language=es&countryCode=PE", timeout.Token);
                if (geo is null || !geo.RootElement.TryGetProperty("results", out var lugares))
                    return SinDatos("sin_ubicacion", "No fue posible verificar la ubicación de esta ciudad para consultar el clima.");
                localidad = lugares.EnumerateArray()
                    .Where(l => l.GetProperty("country_code").GetString() == "PE" &&
                        Normalizar(l.GetProperty("name").GetString() ?? "") == Normalizar(nombre))
                    .OrderByDescending(l => l.TryGetProperty("population", out var p) ? p.GetInt64() : 0)
                    .Select(l => new Localidad(l.GetProperty("latitude").GetDouble(), l.GetProperty("longitude").GetDouble()))
                    .FirstOrDefault();
                if (localidad is null)
                    return SinDatos("sin_ubicacion", "No fue posible verificar la ubicación de esta ciudad para consultar el clima.");
                cache.Set(geoKey, localidad, TimeSpan.FromDays(7));
            }

            var hasta = salida.Date < hoy.AddDays(15) ? salida.Date : hoy.AddDays(15);
            var url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={localidad.Latitud}&longitude={localidad.Longitud}&daily=temperature_2m_min,temperature_2m_max,precipitation_probability_max,weather_code&timezone=America%2FLima&start_date={entrada:yyyy-MM-dd}&end_date={hasta:yyyy-MM-dd}");
            using var forecast = await http.GetFromJsonAsync<JsonDocument>(url, timeout.Token);
            if (forecast is null) return SinDatos("no_disponible", "El servicio meteorológico no está disponible temporalmente.");
            var daily = forecast.RootElement.GetProperty("daily");
            var fechas = daily.GetProperty("time");
            var minimas = daily.GetProperty("temperature_2m_min");
            var maximas = daily.GetProperty("temperature_2m_max");
            var lluvias = daily.GetProperty("precipitation_probability_max");
            var tieneCodigos = daily.TryGetProperty("weather_code", out var codigos) && codigos.ValueKind == JsonValueKind.Array;
            var dias = new List<DiaClimaDto>();
            for (var i = 0; i < fechas.GetArrayLength(); i++)
            {
                var fecha = fechas[i].GetString();
                if (!DateTime.TryParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia) ||
                    dia < entrada.Date || dia > hasta || i >= minimas.GetArrayLength() || i >= maximas.GetArrayLength() ||
                    minimas[i].ValueKind != JsonValueKind.Number || maximas[i].ValueKind != JsonValueKind.Number ||
                    !minimas[i].TryGetDouble(out var min) || !maximas[i].TryGetDouble(out var max) || min > max)
                    continue;
                double? lluvia = i < lluvias.GetArrayLength() && lluvias[i].ValueKind == JsonValueKind.Number &&
                    lluvias[i].TryGetDouble(out var p) && p is >= 0 and <= 100 ? p : null;
                int? codigo = tieneCodigos && i < codigos.GetArrayLength() && codigos[i].ValueKind == JsonValueKind.Number && codigos[i].TryGetInt32(out var wmo)
                    && wmo is >= 0 and <= 99 ? wmo : null;
                dias.Add(new DiaClimaDto(fecha!, min, max, lluvia, codigo));
            }
            dias = dias.DistinctBy(d => d.Fecha).OrderBy(d => d.Fecha).ToList();
            if (dias.Count == 0) return SinDatos("no_disponible", "No se recibieron datos meteorológicos válidos para tu viaje.");
            var completo = dias.Count == (salida.Date - entrada.Date).Days + 1;
            var result = new ClimaViajeDto(completo ? "disponible" : "parcial",
                completo ? "Pronóstico para la ciudad; puede cambiar. Incluye el día de salida."
                    : "Pronóstico parcial: solo hay información para los días mostrados. No cubre toda la estadía.",
                "https://open-meteo.com/", DateTimeOffset.UtcNow, dias);
            cache.Set(clave, result, TimeSpan.FromMinutes(30));
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SinDatos("no_disponible", "La consulta del clima tardó demasiado. Puedes continuar con las opciones de alojamiento.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            logger.LogWarning("No se pudo consultar el clima ({ErrorType}).", ex.GetType().Name);
            return SinDatos("no_disponible", "El servicio meteorológico no está disponible temporalmente.");
        }
    }

    private static ClimaViajeDto SinDatos(string estado, string aviso) => new(estado, aviso, null, null, []);
    private static string Normalizar(string texto) => string.Concat(texto.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        .ToLowerInvariant().Replace("cuzco", "cusco").Replace("nasca", "nazca");
    private sealed record Localidad(double Latitud, double Longitud);
}
