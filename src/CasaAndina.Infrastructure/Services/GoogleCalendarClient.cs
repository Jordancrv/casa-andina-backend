using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace CasaAndina.Infrastructure.Services;

public sealed class CalendarRemoteException(bool reconnect = false) : Exception(
    reconnect ? "Vuelve a conectar tu cuenta de Google Calendar." : "Google Calendar no está disponible. Intenta sincronizar nuevamente.")
{
    public bool Reconnect { get; } = reconnect;
}

public record CalendarTokens(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("scope")] string? Scope);

public sealed class GoogleCalendarClient(HttpClient http, IOptions<GoogleCalendarOptions> options)
{
    public const string Scope = "https://www.googleapis.com/auth/calendar.app.created";
    private const string Api = "https://www.googleapis.com/calendar/v3/";
    public async Task<CalendarTokens> ExchangeAsync(string code, string verifier, CancellationToken ct) =>
        await TokenAsync(new() { ["code"] = code, ["code_verifier"] = verifier,
            ["redirect_uri"] = options.Value.RedirectUri, ["grant_type"] = "authorization_code" }, ct);

    public async Task<CalendarTokens> RefreshAsync(string refreshToken, CancellationToken ct) =>
        await TokenAsync(new() { ["refresh_token"] = refreshToken, ["grant_type"] = "refresh_token" }, ct);

    private async Task<CalendarTokens> TokenAsync(Dictionary<string, string> data, CancellationToken ct)
    {
        data["client_id"] = options.Value.ClientId;
        data["client_secret"] = options.Value.ClientSecret;
        using var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(data), ct);
        if (!response.IsSuccessStatusCode)
        {
            // Do not log the response: it may contain credentials or authorization codes.
            var invalidGrant = false;
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                using var error = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                invalidGrant = error.RootElement.TryGetProperty("error", out var value) && value.GetString() == "invalid_grant";
            }
            throw new CalendarRemoteException(invalidGrant);
        }
        var tokens = await response.Content.ReadFromJsonAsync<CalendarTokens>(cancellationToken: ct);
        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken)) throw new CalendarRemoteException();
        return tokens;
    }

    public async Task<string> EnsureCalendarAsync(string token, string? calendarId, CancellationToken ct)
    {
        if (calendarId is not null)
        {
            using var existing = await SendAsync(HttpMethod.Get, "calendars/" + Uri.EscapeDataString(calendarId), token, null, ct);
            if (existing.IsSuccessStatusCode) return calendarId;
            if (existing.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Gone)) Check(existing);
        }
        using var response = await SendAsync(HttpMethod.Post, "calendars", token,
            new { summary = "Mis estadías · Casa Andina", timeZone = "America/Lima", description = "Reservas gestionadas desde Casa Andina." }, ct);
        Check(response);
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("id").GetString() ?? throw new CalendarRemoteException();
    }

    public async Task<bool> PutEventAsync(string token, string calendarId, string eventId, object body, CancellationToken ct)
    {
        var path = $"calendars/{Uri.EscapeDataString(calendarId)}/events/{eventId}";
        using var response = await SendAsync(HttpMethod.Put, path, token, body, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return false;
        Check(response);
        return true;
    }

    public async Task InsertEventAsync(string token, string calendarId, object body, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, $"calendars/{Uri.EscapeDataString(calendarId)}/events", token, body, ct);
        Check(response);
    }

    public async Task DeleteEventAsync(string token, string calendarId, string eventId, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"calendars/{Uri.EscapeDataString(calendarId)}/events/{eventId}", token, null, ct);
        if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Gone)) Check(response);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        using var response = await http.PostAsync("https://oauth2.googleapis.com/revoke",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refreshToken }), ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest) throw new CalendarRemoteException();
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, Api + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return SendAndDisposeAsync(request, ct);
    }

    private async Task<HttpResponseMessage> SendAndDisposeAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using (request) return await http.SendAsync(request, ct);
    }

    private static void Check(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) throw new CalendarRemoteException(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
    }
}
