using System.Security.Cryptography;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/calendario")]
[Authorize(Policy = "Cliente")]
[EnableRateLimiting("calendario")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CalendarioController(ICalendarioService calendar, ICurrentUserService currentUser,
    IOptions<GoogleCalendarOptions> options) : ControllerBase
{
    private const string CorrelationCookie = "ca-calendar-correlation";
    private int ClienteId => currentUser.ClienteId ?? throw new UnauthorizedAccessException("El token no identifica a un cliente.");

    [HttpGet]
    public async Task<IActionResult> Estado(CancellationToken ct) => Ok(await calendar.EstadoAsync(ClienteId, ct));

    [HttpPost("google/conectar")]
    public IActionResult Conectar()
    {
        var correlation = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var url = calendar.CrearAutorizacion(currentUser.UsuarioId ?? throw new UnauthorizedAccessException(), ClienteId, correlation);
        Response.Cookies.Append(CorrelationCookie, correlation, new CookieOptions {
            HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10), Path = "/api/calendario/google/callback", IsEssential = true });
        return Ok(new { url });
    }

    [AllowAnonymous]
    [HttpGet("google/callback")]
    public async Task<IActionResult> Callback([FromQuery] string? state, [FromQuery] string? code,
        [FromQuery] string? error, CancellationToken ct)
    {
        var correlation = Request.Cookies[CorrelationCookie];
        Response.Cookies.Delete(CorrelationCookie, new CookieOptions { Path = "/api/calendario/google/callback", Secure = Request.IsHttps });
        var result = "error";
        if (error == "access_denied") result = "cancelado";
        else if (!string.IsNullOrWhiteSpace(state) && !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(correlation))
        {
            try
            {
                var clientId = await calendar.CompletarAutorizacionAsync(state, code, correlation, ct);
                result = "conectado";
                await calendar.SincronizarAsync(clientId, ct);
            }
            catch (Exception ex) when (ex is ConflictException or CalendarRemoteException or HttpRequestException or TaskCanceledException) { }
        }
        // The destination is server configuration; never accept a return URL from the browser.
        return Redirect(options.Value.FrontendUrl.TrimEnd('/') + "/cliente/calendario?google=" + result);
    }

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
