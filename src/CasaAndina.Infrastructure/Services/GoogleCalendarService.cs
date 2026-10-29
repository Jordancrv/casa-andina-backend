using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CasaAndina.Infrastructure.Services;

public sealed class GoogleCalendarService(ApplicationDbContext db, GoogleCalendarClient google,
    IOptions<GoogleCalendarOptions> options, IDataProtectionProvider protection, IMemoryCache cache,
    CalendarSyncLock syncLock) : ICalendarioService
{
    private readonly IDataProtector protector = protection.CreateProtector("CasaAndina.GoogleCalendar.RefreshToken.v1");
    private record Pending(int UsuarioId, int ClienteId, string Verifier, string CorrelationHash);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public async Task<EstadoCalendario> EstadoAsync(int clienteId, CancellationToken ct)
    {
        if (!options.Value.Configurado) return new(false, false, false, null, null);
        var connection = await db.Set<ConexionCalendario>().AsNoTracking().SingleOrDefaultAsync(x => x.ClienteId == clienteId, ct);
        var connected = connection?.RefreshTokenProtegido is not null;
        return new(true, connected, connection?.RequiereReconectar ?? false, connection?.UltimaSincronizacionUtc,
            connected && connection?.CalendarioId is not null ? "https://calendar.google.com/calendar/u/0/r?cid=" + Uri.EscapeDataString(connection.CalendarioId) : null);
    }

    public string CrearAutorizacion(int usuarioId, int clienteId, string correlacion)
    {
        CheckConfig();
        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        cache.Set("calendar:" + state, new Pending(usuarioId, clienteId, verifier, Hash(correlacion)), TimeSpan.FromMinutes(10));
        return QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string?> {
            ["client_id"] = options.Value.ClientId, ["redirect_uri"] = options.Value.RedirectUri,
            ["response_type"] = "code", ["scope"] = GoogleCalendarClient.Scope, ["access_type"] = "offline",
            ["prompt"] = "consent", ["state"] = state, ["code_challenge_method"] = "S256",
            ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))) });
    }

    public async Task<int> CompletarAutorizacionAsync(string state, string code, string correlacion, CancellationToken ct)
    {
        CheckConfig();
        await syncLock.Gate.WaitAsync(ct);
        try
        {
            if (!cache.TryGetValue<Pending>("calendar:" + state, out var pending) || pending is null
                || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(pending.CorrelationHash), Encoding.ASCII.GetBytes(Hash(correlacion))))
                throw new ConflictException("La autorización expiró. Inicia nuevamente la conexión.");
            cache.Remove("calendar:" + state);
            if (!await db.Usuarios.AnyAsync(u => u.Id == pending.UsuarioId && u.Activo
                    && u.Persona.Cliente != null && u.Persona.Cliente.Id == pending.ClienteId
                    && u.Roles.Any(r => r.Rol.Codigo == "CLIENTE"), ct))
                throw new ConflictException("La cuenta ya no está disponible.");
            var tokens = await google.ExchangeAsync(code, pending.Verifier, ct);
            if (tokens.Scope?.Split(' ').Contains(GoogleCalendarClient.Scope) != true || string.IsNullOrEmpty(tokens.RefreshToken))
                throw new ConflictException("Autoriza el permiso para crear y gestionar el calendario de Casa Andina.");
            var connection = await db.Set<ConexionCalendario>().SingleOrDefaultAsync(x => x.ClienteId == pending.ClienteId, ct);
            if (connection is null) { connection = new() { ClienteId = pending.ClienteId }; db.Add(connection); }
            var calendarId = await google.EnsureCalendarAsync(tokens.AccessToken, connection.CalendarioId, ct);
            if (connection.CalendarioId != calendarId)
            {
                db.RemoveRange(await db.Set<EventoCalendario>().Where(x => x.ClienteId == pending.ClienteId).ToListAsync(ct));
                connection.UltimaSincronizacionUtc = null;
            }
            connection.CalendarioId = calendarId;
            connection.RefreshTokenProtegido = protector.Protect(tokens.RefreshToken);
            connection.RequiereReconectar = false;
            await db.SaveChangesAsync(ct);
            return pending.ClienteId;
        }
        finally { syncLock.Gate.Release(); }
    }

    public async Task<ResultadoSincronizacion> SincronizarAsync(int clienteId, CancellationToken ct)
    {
        CheckConfig();
        await syncLock.Gate.WaitAsync(ct);
        ConexionCalendario? connection = null;
        try
        {
            connection = await db.Set<ConexionCalendario>().SingleOrDefaultAsync(x => x.ClienteId == clienteId, ct);
            if (connection?.RefreshTokenProtegido is null || connection.RequiereReconectar)
                throw new ConflictException("Conecta tu cuenta de Google Calendar para sincronizar.");
            var tokens = await google.RefreshAsync(protector.Unprotect(connection.RefreshTokenProtegido), ct);
            if (!string.IsNullOrEmpty(tokens.RefreshToken)) connection.RefreshTokenProtegido = protector.Protect(tokens.RefreshToken);
            var calendarId = await google.EnsureCalendarAsync(tokens.AccessToken, connection.CalendarioId, ct);
            if (calendarId != connection.CalendarioId)
            {
                db.RemoveRange(await db.Set<EventoCalendario>().Where(x => x.ClienteId == clienteId).ToListAsync(ct));
                connection.CalendarioId = calendarId;
                await db.SaveChangesAsync(ct);
            }
            var reservations = await db.Reservas.AsNoTracking().Where(r => r.ClienteId == clienteId && r.Habitaciones.Any())
                .Include(r => r.Sede).Include(r => r.Habitaciones).ToListAsync(ct);
            var events = await db.Set<EventoCalendario>().Where(x => x.ClienteId == clienteId).ToDictionaryAsync(x => x.ReservaId, ct);
            var active = reservations.Where(r => r.Estado is "Reservado" or "Completada").ToList();
            var activeIds = active.Select(r => r.Id).ToHashSet();
            var removed = 0;
            foreach (var obsolete in events.Values.Where(e => !activeIds.Contains(e.ReservaId)))
            {
                await google.DeleteEventAsync(tokens.AccessToken, calendarId, obsolete.EventoId, ct);
                db.Remove(obsolete);
                await db.SaveChangesAsync(ct);
                removed++;
            }
            var updated = 0;
            foreach (var reservation in active)
            {
                var payload = CrearEvento(reservation);
                var fingerprint = Hash(JsonSerializer.Serialize(payload));
                events.TryGetValue(reservation.Id, out var saved);
                if (saved?.Huella == fingerprint) continue;
                if (saved is null)
                {
                    saved = new() { ClienteId = clienteId, ReservaId = reservation.Id };
                    db.Add(saved);
                    await db.SaveChangesAsync(ct); // Stable id survives a timeout after Google creates the event.
                }
                if (!await google.PutEventAsync(tokens.AccessToken, calendarId, saved.EventoId, payload, ct))
                {
                    saved.EventoId = Guid.NewGuid().ToString("N");
                    await db.SaveChangesAsync(ct);
                    await google.InsertEventAsync(tokens.AccessToken, calendarId, new {
                        id = saved.EventoId, payload.summary, payload.location, payload.description,
                        payload.start, payload.end, payload.reminders, payload.visibility }, ct);
                }
                saved.Huella = fingerprint;
                await db.SaveChangesAsync(ct);
                updated++;
            }
            connection.UltimaSincronizacionUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new(updated, removed, connection.UltimaSincronizacionUtc.Value);
        }
        catch (CryptographicException)
        {
            if (connection is not null) { connection.RequiereReconectar = true; await db.SaveChangesAsync(ct); }
            throw new CalendarRemoteException(true);
        }
        catch (CalendarRemoteException ex) when (ex.Reconnect)
        {
            if (connection is not null) { connection.RequiereReconectar = true; await db.SaveChangesAsync(ct); }
            throw;
        }
        finally { syncLock.Gate.Release(); }
    }

    public static CalendarEvent CrearEvento(Reserva r) => new(
        $"Estadía · {r.Sede.Nombre}", string.Join(", ", new[] { r.Sede.Direccion, r.Sede.Ciudad }.Where(x => !string.IsNullOrWhiteSpace(x))),
        $"Reserva {r.CodigoReserva}\nEntrada: {r.Habitaciones.Min(x => x.FechaCheckIn):dd/MM/yyyy}\nSalida: {r.Habitaciones.Max(x => x.FechaCheckOut):dd/MM/yyyy}\nGestiona tu reserva desde Casa Andina. La salida marca el fin de la estadía.",
        new(r.Habitaciones.Min(x => x.FechaCheckIn).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        new(r.Habitaciones.Max(x => x.FechaCheckOut).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        new(false, [new("popup", 1440)]));
    public record CalendarDate(string date);
    public record Reminder(string method, int minutes);
    public record Reminders(bool useDefault, Reminder[] overrides);
    public record CalendarEvent(string summary, string location, string description, CalendarDate start, CalendarDate end, Reminders reminders, string visibility = "private");

    public async Task DesconectarAsync(int clienteId, CancellationToken ct)
    {
        await syncLock.Gate.WaitAsync(ct);
        try
        {
            var connection = await db.Set<ConexionCalendario>().SingleOrDefaultAsync(x => x.ClienteId == clienteId, ct);
            if (connection?.RefreshTokenProtegido is null) return;
            try { await google.RevokeAsync(protector.Unprotect(connection.RefreshTokenProtegido), ct); }
            catch (CryptographicException) { /* Locally unreadable tokens cannot be used again. */ }
            connection.RefreshTokenProtegido = null;
            connection.RequiereReconectar = false;
            await db.SaveChangesAsync(ct);
        }
        finally { syncLock.Gate.Release(); }
    }

    private void CheckConfig()
    {
        if (!options.Value.Configurado) throw new ConflictException("Google Calendar aún no está configurado en el servidor.");
    }
}
