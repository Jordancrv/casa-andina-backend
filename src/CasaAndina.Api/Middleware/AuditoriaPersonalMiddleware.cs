using System.Text.Json;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Infrastructure.Persistence;
using CasaAndina.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Api.Middleware;

/// <summary>Registra actividad HTTP exitosa del personal sin almacenar cuerpos, consultas ni secretos.</summary>
public sealed class AuditoriaPersonalMiddleware(RequestDelegate next, ILogger<AuditoriaPersonalMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext http, ICurrentUserService actor, IServiceScopeFactory scopes)
    {
        var started = DateTime.UtcNow;
        await next(http);
        if (http.Response.StatusCode is < 200 or >= 300 || actor.UsuarioId is not int userId || actor.SedeId is not int sedeId || actor.ClienteId is not null ||
            !http.User.HasClaim(AuthClaimTypes.TipoUsuario, AuthClaimTypes.Personal))
            return;
        var action = http.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action is null || action.ControllerName is "Auth" or "Auditoria" ||
            (action.ControllerName == "Accesos" && action.ActionName == "Actual")) return;
        var method = http.Request.Method;
        if (method is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE")) return;
        try
        {
            // Las operaciones con auditoría de dominio ya incluyen más contexto y se
            // guardaron junto al cambio; no repetirlas como eventos HTTP genéricos.
            var originalDb = http.RequestServices.GetService<IApplicationDbContext>() as ApplicationDbContext;
            if (method != "GET" && originalDb is not null && originalDb.ChangeTracker.Entries<Auditoria>()
                .Any(e => e.Entity.UsuarioId == userId && e.Entity.SedeId == sedeId && e.Entity.FechaUtc >= started)) return;

            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var id = http.Request.RouteValues.TryGetValue("id", out var raw) && long.TryParse(Convert.ToString(raw), out var numericId)
                ? numericId.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-";
            db.Auditoria.Add(new Auditoria { UsuarioId = userId, SedeId = sedeId, FechaUtc = DateTime.UtcNow,
                Accion = method switch { "GET" => "OTRA", "POST" => "INSERT", "DELETE" => "DELETE", _ => "UPDATE" },
                Entidad = action.ControllerName, EntidadId = id,
                Despues = JsonSerializer.Serialize(new { operacion = action.ActionName, metodo = method }),
                DireccionIp = http.Connection.RemoteIpAddress?.MapToIPv6().ToString().Split('%')[0] });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // La respuesta de negocio puede haberse enviado; no alterar un cambio ya confirmado.
            logger.LogError(ex, "No se pudo registrar la actividad de personal para el usuario {UsuarioId}.", userId);
        }
    }
}
