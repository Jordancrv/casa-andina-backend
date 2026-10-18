using System.Net.Http.Json;
using System.Text.Json;
using CasaAndina.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasaAndina.Infrastructure.Services;

public sealed class GoogleRecaptchaVerifier(
    HttpClient httpClient,
    IOptions<GoogleRecaptchaOptions> options,
    IHostEnvironment environment,
    ILogger<GoogleRecaptchaVerifier> logger) : IRecaptchaVerifier
{
    private const string VerifyUrl = "https://www.google.com/recaptcha/api/siteverify";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            if (!environment.IsProduction()) return true;
            logger.LogError("Google reCAPTCHA está deshabilitado en producción.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(settings.SecretKey)
            || settings.AllowedHostnames.Length == 0)
            return false;

        try
        {
            using var response = await httpClient.PostAsync(
                VerifyUrl,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = settings.SecretKey,
                    ["response"] = token
                }),
                cancellationToken);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<GoogleVerificationResponse>(JsonOptions, cancellationToken);
            return result is { Success: true, Hostname: not null }
                && settings.AllowedHostnames.Any(hostname => string.Equals(
                    hostname.Trim(), result.Hostname, StringComparison.OrdinalIgnoreCase));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "No se pudo verificar el token de Google reCAPTCHA.");
            return false;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "La verificación de Google reCAPTCHA excedió el tiempo límite.");
            return false;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Google reCAPTCHA devolvió una respuesta no válida.");
            return false;
        }
    }

    private sealed record GoogleVerificationResponse(bool Success, string? Hostname);
}
