using System.Security.Cryptography;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/calendario")]
[Authorize(Policy = "Cliente")]
[EnableRateLimiting("calendario")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CalendarioController(ICalendarioService calendar, ICurrentUserService currentUser,
    IOptions<GoogleCalendarOptions> options, IMemoryCache cache) : ControllerBase
{
    private sealed record CallbackPendiente(string State, string Code);
    public sealed record ConectarRequest(string Correlacion);
    public sealed record FinalizarRequest(string Ticket, string Correlacion);
    private int ClienteId => currentUser.ClienteId ?? throw new UnauthorizedAccessException("El token no identifica a un cliente.");

    [HttpGet]
    public async Task<IActionResult> Estado(CancellationToken ct) => Ok(await calendar.EstadoAsync(ClienteId, ct));

    [HttpPost("google/conectar")]
    public IActionResult Conectar([FromBody] ConectarRequest request)
    {
        if (!CorrelacionValida(request.Correlacion)) return BadRequest();
        var url = calendar.CrearAutorizacion(currentUser.UsuarioId ?? throw new UnauthorizedAccessException(), ClienteId, request.Correlacion);
        return Ok(new { url });
    }

    [AllowAnonymous]
    [HttpGet("google/callback")]
    public IActionResult Callback([FromQuery] string? state, [FromQuery] string? code, [FromQuery] string? error)
    {
        var frontend = options.Value.FrontendUrl.TrimEnd('/') + "/cliente/calendario";
        if (error == "access_denied") return Redirect(frontend + "?google=cancelado");
        if (string.IsNullOrWhiteSpace(state) || state.Length > 128 || string.IsNullOrWhiteSpace(code) || code.Length > 4096)
            return Redirect(frontend + "?google=error");
        var ticket = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        cache.Set("calendar-callback:" + ticket, new CallbackPendiente(state, code), TimeSpan.FromMinutes(5));
        // El código OAuth nunca llega al navegador de la aplicación; solo viaja este identificador efímero.
        return Redirect(frontend + "?google_ticket=" + ticket);
    }

    [HttpPost("google/finalizar")]
    public async Task<IActionResult> Finalizar([FromBody] FinalizarRequest request, CancellationToken ct)
    {
        if (!CorrelacionValida(request.Ticket) || !CorrelacionValida(request.Correlacion)) return BadRequest();
        CallbackPendiente? pending;
        lock (cache)
        {
            cache.TryGetValue("calendar-callback:" + request.Ticket, out pending);
            cache.Remove("calendar-callback:" + request.Ticket);
        }
        if (pending is null) throw new ConflictException("La autorización expiró. Inicia nuevamente la conexión.");
        var clientId = await calendar.CompletarAutorizacionAsync(pending.State, pending.Code, request.Correlacion,
            currentUser.UsuarioId ?? throw new UnauthorizedAccessException(), ClienteId, ct);
        try { await calendar.SincronizarAsync(clientId, ct); }
        catch (Exception ex) when (ex is CalendarRemoteException or HttpRequestException or TaskCanceledException) { return Ok(new { sincronizado = false }); }
        return Ok(new { sincronizado = true });
    }

    private static bool CorrelacionValida(string? value) => value is { Length: 43 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    [HttpPost("sincronizar")]
    public async Task<IActionResult> Sincronizar(CancellationToken ct)
    {
        try { return Ok(await calendar.SincronizarAsync(ClienteId, ct)); }
        catch (Exception ex) when (ex is CalendarRemoteException or HttpRequestException or TaskCanceledException)
        { return Problem(statusCode: 503, title: ex is CalendarRemoteException ? ex.Message : "No se pudo contactar a Google Calendar. Intenta nuevamente."); }
    }

    [HttpDelete("google")]
    public async Task<IActionResult> Desconectar(CancellationToken ct)
    {
        try { await calendar.DesconectarAsync(ClienteId, ct); return NoContent(); }
        catch (Exception ex) when (ex is CalendarRemoteException or HttpRequestException or TaskCanceledException)
        { return Problem(statusCode: 503, title: "No pudimos revocar la conexión con Google. Intenta nuevamente."); }
    }
}
