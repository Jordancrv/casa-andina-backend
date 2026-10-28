namespace CasaAndina.Infrastructure.Services;

public sealed class GoogleCalendarOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "http://localhost:51141/api/calendario/google/callback";
    public string FrontendUrl { get; set; } = "http://localhost:5173";
    public string? DataProtectionKeysPath { get; set; }
    public bool Configurado => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret)
        && Uri.TryCreate(RedirectUri, UriKind.Absolute, out var callback) && EsWebSegura(callback)
        && Uri.TryCreate(FrontendUrl, UriKind.Absolute, out var frontend) && EsWebSegura(frontend);
    private static bool EsWebSegura(Uri uri) => uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback);
}

// Serializa las conexiones y sincronizaciones dentro de una instancia de la API.
public sealed class CalendarSyncLock
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}
