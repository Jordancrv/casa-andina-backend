using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Api.Controllers;

[ApiController, Route("api/auditoria"), Authorize(Policy = "Administrador")]
public sealed class AuditoriaController(IApplicationDbContext db, ICurrentUserService user) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int pagina = 1, [FromQuery] string? buscar = null,
        [FromQuery] string? accion = null, [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null,
        CancellationToken ct = default)
    {
        if (user.Rol != RolUsuario.Administrador || user.SedeId is not int sede || user.UsuarioId is null)
            throw new ForbiddenAccessException("Solo el administrador de la sede puede consultar la auditoría.");
        if (pagina is < 1 or > 1000000 || buscar?.Length > 100 ||
            (accion is not null && accion is not ("INSERT" or "UPDATE" or "DELETE" or "OTRA" or "LOGIN" or "LOGOUT")) ||
            desde?.TimeOfDay > TimeSpan.Zero || hasta?.TimeOfDay > TimeSpan.Zero || (desde.HasValue && hasta.HasValue && desde > hasta) ||
            (hasta.HasValue && hasta.Value.Date == DateTime.MaxValue.Date))
            return BadRequest(new { detail = "Los filtros de auditoría no son válidos." });
        var query = db.Auditoria.AsNoTracking().Where(a => a.SedeId == sede && ((a.UsuarioId == null && a.Entidad == "ReservaHabitacion") || (a.UsuarioId != null && a.Usuario != null
            && a.Usuario.Persona.Cliente == null && !a.Usuario.Roles.Any(r => r.Rol.Codigo == "CLIENTE"))));
        if (accion is not null) query = query.Where(a => a.Accion == accion);
        if (desde.HasValue) { var utc = PeruBusinessTime.UtcStartOfDay(desde.Value.Date); query = query.Where(a => a.FechaUtc >= utc); }
        if (hasta.HasValue) { var utc = PeruBusinessTime.UtcStartOfDay(hasta.Value.Date.AddDays(1)); query = query.Where(a => a.FechaUtc < utc); }
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim();
            query = query.Where(a => a.Entidad.Contains(term) || (a.Usuario == null && "Sistema automático".Contains(term)) || (a.Usuario != null && (a.Usuario.Correo.Contains(term) ||
                (a.Usuario.Persona.Natural != null && (a.Usuario.Persona.Natural.Nombres.Contains(term) ||
                    (a.Usuario.Persona.Natural.Apellidos != null && a.Usuario.Persona.Natural.Apellidos.Contains(term)))))));
        }
        var rows = await query.OrderByDescending(a => a.FechaUtc).ThenByDescending(a => a.Id).Skip((pagina - 1) * 20).Take(21)
            .Select(a => new { a.Id, a.FechaUtc, a.Accion, a.Entidad, a.EntidadId,
                Usuario = a.Usuario == null ? "Sistema automático" : a.Usuario.Persona.Natural != null ? (a.Usuario.Persona.Natural.Nombres + " " + (a.Usuario.Persona.Natural.Apellidos ?? "")).Trim() : "Personal",
                Correo = a.Usuario == null ? "" : a.Usuario.Correo, a.Despues }).ToListAsync(ct);
        Response.Headers.CacheControl = "no-store, private";
        return Ok(new { items = rows.Take(20).Select(a => new { a.Id, a.FechaUtc, a.Accion, a.Entidad, a.EntidadId, a.Usuario, a.Correo,
            Operacion = Operation(a.Despues) }), pagina, haySiguiente = rows.Count > 20 });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalle(long id, CancellationToken ct)
    {
        if (user.Rol != RolUsuario.Administrador || user.SedeId is not int sede || user.UsuarioId is null)
            throw new ForbiddenAccessException("Solo el administrador de la sede puede consultar la auditoría.");
        var row = await db.Auditoria.AsNoTracking().Where(a => a.Id == id && a.SedeId == sede && ((a.UsuarioId == null && a.Entidad == "ReservaHabitacion") || (a.UsuarioId != null && a.Usuario != null
            && a.Usuario.Persona.Cliente == null && !a.Usuario.Roles.Any(r => r.Rol.Codigo == "CLIENTE"))))
            .Select(a => new { a.Id, a.FechaUtc, a.Accion, a.Entidad, a.EntidadId, a.Antes, a.Despues,
                Usuario = a.Usuario == null ? "Sistema automático" : a.Usuario.Persona.Natural != null ? (a.Usuario.Persona.Natural.Nombres + " " + (a.Usuario.Persona.Natural.Apellidos ?? "")).Trim() : "Personal", Correo = a.Usuario == null ? "" : a.Usuario.Correo })
            .SingleOrDefaultAsync(ct);
        Response.Headers.CacheControl = "no-store, private";
        return row is null ? NotFound() : Ok(new { row.Id, row.FechaUtc, row.Accion, row.Entidad, row.EntidadId, row.Usuario, row.Correo,
            Operacion = Operation(row.Despues), Antes = SafeDetails(row.Entidad, row.Antes), Despues = SafeDetails(row.Entidad, row.Despues) });
    }

    private sealed record CampoDetalle(string Campo, string Valor);
    private static IReadOnlyList<CampoDetalle> SafeDetails(string entidad, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (entidad == "UsuarioRoles" && root.ValueKind == System.Text.Json.JsonValueKind.Array)
                return [new("Roles (ID)", string.Join(", ", root.EnumerateArray().Where(x => x.ValueKind == System.Text.Json.JsonValueKind.Number && x.TryGetInt32(out _)).Select(x => x.GetInt32()).Take(20)))];
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return [];
            var allowed = entidad switch
            {
                "Usuario" => new Dictionary<string, string> { ["estado"] = "Estado", ["Roles"] = "Roles (ID)" },
                "Reserva" => new Dictionary<string, string> { ["Estado"] = "Estado", ["PrecioTotal"] = "Importe reservado", ["Devolucion"] = "Devolución" },
                "ReservaHabitacion" => new Dictionary<string, string> { ["Movimiento"] = "Movimiento", ["Origen"] = "Origen", ["FechaUtc"] = "Fecha efectiva (UTC)", ["Recuperado"] = "Recuperado tras inactividad" },
                "Rol" => new Dictionary<string, string> { ["Nombre"] = "Nombre del rol", ["Activo"] = "Activo", ["Permisos"] = "Permisos" },
                _ => new Dictionary<string, string>()
            };
            var fields = new List<CampoDetalle>();
            foreach (var (key, label) in allowed)
            {
                if (!root.TryGetProperty(key, out var value)) continue;
                string? text = value.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => value.GetString(),
                    System.Text.Json.JsonValueKind.Number or System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => value.ToString(),
                    System.Text.Json.JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Where(x => x.ValueKind is System.Text.Json.JsonValueKind.String or System.Text.Json.JsonValueKind.Number).Take(100).Select(x => x.ToString())),
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(text)) fields.Add(new(label, text.Length > 3000 ? text[..3000] : text));
            }
            return fields;
        }
        catch (System.Text.Json.JsonException) { return []; }
    }

    private static string? Operation(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            if (doc.RootElement.TryGetProperty("operacion", out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                return value.GetString();
            if (doc.RootElement.TryGetProperty("Movimiento", out var movimiento) && movimiento.ValueKind == System.Text.Json.JsonValueKind.String)
                return movimiento.GetString() switch { "CHECK_IN" => "CheckIn", "CHECK_OUT" => "CheckOut", _ => null };
            if (doc.RootElement.TryGetProperty("estado", out var estado) && estado.ValueKind == System.Text.Json.JsonValueKind.String)
                return estado.GetString() switch { "Activo" => "Activate", "Inactivo" => "Deactivate", _ => null };
            return null;
        }
        catch (System.Text.Json.JsonException) { return null; }
    }
}
