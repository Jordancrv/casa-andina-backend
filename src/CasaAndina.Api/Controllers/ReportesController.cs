using System.Text.Json;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Dashboard.Queries;
using CasaAndina.Application.Reportes;
using CasaAndina.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize(Policy = "Administrador")]
public sealed class ReportesController(ISender sender, ICurrentUserService user, IAnalisisReporteService analysis, IAnalisisReportePdfService pdf, IApplicationDbContext db) : ControllerBase
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private int Sede() => user.SedeId ?? throw new ForbiddenAccessException("El análisis requiere una sede asignada.");
    private static object ResponseData(ReporteAnalisis row)
    {
        var data = JsonSerializer.Deserialize<AnalisisReporte>(row.ContenidoJson, Json) ?? throw new InvalidOperationException("El reporte guardado no es válido.");
        return new { id = row.Id, data.Resumen, data.Hallazgos, data.Recomendaciones, data.Limitaciones, data.GeneradoUtc, data.Datos };
    }
    [HttpPost("analisis")]
    public async Task<IActionResult> Analizar([FromQuery] string periodo = "30d", CancellationToken cancellationToken = default)
    {
        var sedeId = Sede();
        if (user.UsuarioId is not int usuarioId) throw new ForbiddenAccessException("El análisis requiere un usuario válido.");
        if (periodo is not ("7d" or "30d" or "mes")) return BadRequest(new { detail = "Periodo inválido." });
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var latest = await db.ReportesAnalisis.AsNoTracking().Where(x => x.SedeId == sedeId && x.Periodo == periodo && x.GeneradoUtc >= DateTime.UtcNow.AddHours(-6)).OrderByDescending(x => x.GeneradoUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            if (latest is not null) return Ok(ResponseData(latest));
            var dashboard = await sender.Send(new GetDashboardBiQuery(periodo), cancellationToken);
            var result = await analysis.ObtenerAsync(sedeId, periodo, dashboard, cancellationToken);
            if (result is null) return StatusCode(503, new { detail = "El análisis no está disponible. Inténtalo más tarde." });
            var row = new ReporteAnalisis { SedeId = sedeId, UsuarioId = usuarioId, Periodo = periodo, GeneradoUtc = result.GeneradoUtc, ContenidoJson = JsonSerializer.Serialize(result, Json) };
            db.ReportesAnalisis.Add(row);
            await db.SaveChangesAsync(cancellationToken);
            Response.Headers.CacheControl = "no-store, private";
            return Ok(ResponseData(row));
        }
        finally { Gate.Release(); }
    }
    [HttpGet("analisis")]
    public async Task<IActionResult> Historial([FromQuery] string periodo = "30d", [FromQuery] int pagina = 1, CancellationToken ct = default)
    {
        var sedeId = Sede();
        if (periodo is not ("7d" or "30d" or "mes") || pagina < 1 || pagina > 1000000) return BadRequest(new { detail = "Periodo o página inválidos." });
        var rows = await db.ReportesAnalisis.AsNoTracking().Where(x => x.SedeId == sedeId && x.Periodo == periodo)
            .OrderByDescending(x => x.GeneradoUtc).ThenByDescending(x => x.Id).Skip((pagina - 1) * 20).Take(21)
            .Select(x => new { x.Id, x.Periodo, x.GeneradoUtc }).ToListAsync(ct);
        Response.Headers.CacheControl = "no-store, private";
        return Ok(new { items = rows.Take(20), haySiguiente = rows.Count > 20 });
    }
    [HttpGet("analisis/{id:long}")]
    public async Task<IActionResult> Detalle(long id, CancellationToken ct)
    {
        var sedeId = Sede();
        var row = await db.ReportesAnalisis.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SedeId == sedeId, ct);
        Response.Headers.CacheControl = "no-store, private";
        return row is null ? NotFound() : Ok(ResponseData(row));
    }
    [HttpGet("analisis/{id:long}/pdf")]
    public async Task<IActionResult> Descargar(long id, CancellationToken ct)
    {
        var sedeId = Sede();
        var row = await db.ReportesAnalisis.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SedeId == sedeId, ct);
        if (row is null) return NotFound();
        var data = JsonSerializer.Deserialize<AnalisisReporte>(row.ContenidoJson, Json) ?? throw new InvalidOperationException("El reporte guardado no es válido.");
        Response.Headers.CacheControl = "no-store, private";
        return File(pdf.Generar(data), "application/pdf", $"Analisis-CasaAndina-{row.Id}.pdf");
    }
}
