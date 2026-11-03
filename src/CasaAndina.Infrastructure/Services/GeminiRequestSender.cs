using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace CasaAndina.Infrastructure.Services;

/// <summary>Prueba cada clave como máximo una vez; solo cambia de clave ante HTTP 429.</summary>
internal static class GeminiRequestSender
{
    public static async Task<HttpResponseMessage?> SendAsync(HttpClient http, GeminiOptions options, object payload, CancellationToken ct)
    {
        var keys = new[] { options.ApiKey }.Concat(options.ApiKeys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key)).Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal).Take(10).ToArray();
        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            if (options.Cooldowns.TryGetValue(fingerprint, out var until) && until > DateTimeOffset.UtcNow) continue;
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{options.Model}:generateContent");
            request.Headers.Add("x-goog-api-key", key);
            request.Content = JsonContent.Create(payload);
            var response = await http.SendAsync(request, ct);
            if (response.StatusCode != HttpStatusCode.TooManyRequests) return response;
            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1);
            if (response.Headers.RetryAfter?.Date is { } date) delay = date - DateTimeOffset.UtcNow;
            delay = TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 60, 900));
            options.Cooldowns[fingerprint] = DateTimeOffset.UtcNow.Add(delay);
            response.Dispose();
        }
        return null;
    }
}
