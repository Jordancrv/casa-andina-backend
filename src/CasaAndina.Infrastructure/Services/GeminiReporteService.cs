using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CasaAndina.Application.Dashboard.DTOs;
using CasaAndina.Application.Reportes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CasaAndina.Infrastructure.Services;

public sealed class GeminiReporteService(IHttpClientFactory factory, IOptions<GeminiOptions> options, IMemoryCache cache) : IAnalisisReporteService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Respuesta(string Resumen, string Hallazgos, string Recomendaciones, string Limitaciones);
    public AnalisisReporte? Consultar(int sedeId, string periodo) => cache.TryGetValue($"reporte-ia:v1:{sedeId}:{periodo}", out AnalisisReporte? result) ? result : null;
    public async Task<AnalisisReporte?> ObtenerAsync(int sedeId, string periodo, DashboardBiDto datos, CancellationToken cancellationToken)
    {
        var key = $"reporte-ia:v1:{sedeId}:{periodo}";
        if (cache.TryGetValue(key, out AnalisisReporte? existing)) return existing;
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(key, out existing)) return existing;
            var config = options.Value;
            if (string.IsNullOrWhiteSpace(config.ApiKey) || !Regex.IsMatch(config.Model, "^[a-zA-Z0-9.-]+$")) return null;
            var aggregates = new { datos.Kpis, datos.ReservasSemana, datos.CanceladasPeriodo, datos.Canales, datos.Periodo };
            const string instructions = "Analiza en español los indicadores agregados de un hotel para un administrador. Devuelve JSON con resumen, hallazgos, recomendaciones y limitaciones, cada campo texto breve. Compara con el periodo anterior cuando exista base; si es cero no inventes porcentajes. Ocupación es actual; serie semanal cubre seis semanas. Importe reservado no equivale a cobros. No inventes causas, predicciones, datos, objetivos ni hechos externos. Distingue observaciones de sugerencias y explica límites. Usa solo los datos proporcionados.";
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{config.Model}:generateContent");
            request.Headers.Add("x-goog-api-key", config.ApiKey);
            request.Content = JsonContent.Create(new { systemInstruction = new { parts = new[] { new { text = instructions } } }, contents = new[] { new { role = "user", parts = new[] { new { text = JsonSerializer.Serialize(aggregates, Json) } } } }, generationConfig = new { temperature = 0.2, maxOutputTokens = 1600, responseMimeType = "application/json" } });
            using var response = await factory.CreateClient("GeminiReporte").SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var candidate = document.RootElement.GetProperty("candidates")[0];
            if (candidate.GetProperty("finishReason").GetString() != "STOP") return null;
            var parts = candidate.GetProperty("content").GetProperty("parts");
            var text = string.Concat(parts.EnumerateArray().Where(p => !p.TryGetProperty("thought", out var thought) || !thought.GetBoolean()).Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString()));
            var result = JsonSerializer.Deserialize<Respuesta>(text, Json);
            if (result is null || new[] { result.Resumen, result.Hallazgos, result.Recomendaciones, result.Limitaciones }.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 2500)) return null;
            var report = new AnalisisReporte(result.Resumen, result.Hallazgos, result.Recomendaciones, result.Limitaciones, DateTime.UtcNow, datos);
            cache.Set(key, report, TimeSpan.FromHours(6));
            return report;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException) { return null; }
        finally { Gate.Release(); }
    }
}
