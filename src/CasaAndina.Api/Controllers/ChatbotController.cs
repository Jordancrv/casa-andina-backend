using CasaAndina.Application.Chatbot.Queries;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Chatbot.DTOs;
using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CasaAndina.Api.Controllers;

/// <summary>RF05/RF06: expuesto al Portal Cliente (requiere JWT de cliente).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Cliente")]
public class ChatbotController : ControllerBase

{
    private readonly ISender _mediator;

    public ChatbotController(ISender mediator) => _mediator = mediator;

    [HttpGet("recomendaciones")]
    public async Task<IActionResult> GetRecomendaciones([FromQuery] GetRecomendacionesQuery query, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [HttpPost("planificar")]
    [EnableRateLimiting("asistente")]
    public async Task<IActionResult> Planificar([FromBody] PlanificarViajeQuery query, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [HttpPost("conversar")]
    [EnableRateLimiting("asistente")]
    public async Task<IActionResult> Conversar([FromBody] ConversarViajeQuery query,
        [FromServices] IApplicationDbContext db, [FromServices] ICurrentUserService user, CancellationToken cancellationToken)
    {
        if (query.Preferencias is null)
        {
            var memoria = await db.Clientes.Where(c => c.Id == user.ClienteId)
                .Select(c => new Preferencia(c.RecordarPlaya, c.RecordarEvitarLluvia)).SingleAsync(cancellationToken);
            query = query with { Preferencias = new PreferenciasConversacionDto(PrefierePlaya: memoria.Playa, EvitarLluvia: memoria.EvitarLluvia) };
        }
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    public sealed record Preferencia(bool Playa, bool EvitarLluvia);
    public sealed record Valoracion(bool Util, [MaxLength(500)] string? Motivo,
        [MaxLength(100)] string? Ciudad, DateTimeOffset PlanGeneradoEn);

    [HttpGet("preferencias")]
    public async Task<IActionResult> Preferencias([FromServices] IApplicationDbContext db, [FromServices] ICurrentUserService user, CancellationToken ct)
        => Ok(await db.Clientes.Where(c => c.Id == user.ClienteId)
            .Select(c => new Preferencia(c.RecordarPlaya, c.RecordarEvitarLluvia)).SingleAsync(ct));

    [HttpPut("preferencias")]
    public async Task<IActionResult> GuardarPreferencias(Preferencia datos, [FromServices] IApplicationDbContext db,
        [FromServices] ICurrentUserService user, CancellationToken ct)
    {
        var cliente = await db.Clientes.SingleAsync(c => c.Id == user.ClienteId, ct);
        cliente.RecordarPlaya = datos.Playa;
        cliente.RecordarEvitarLluvia = datos.EvitarLluvia;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("valoracion")]
    public async Task<IActionResult> Valorar(Valoracion datos, [FromServices] IApplicationDbContext db,
        [FromServices] ICurrentUserService user, CancellationToken ct)
    {
        if (user.ClienteId is not int clienteId) return Forbid();
        if (datos.PlanGeneradoEn == default) return BadRequest();
        var voto = await db.ValoracionesViaje.SingleOrDefaultAsync(v => v.ClienteId == clienteId && v.PlanGeneradoEn == datos.PlanGeneradoEn, ct);
        if (voto == null)
        {
            voto = new ValoracionViaje { ClienteId = clienteId, PlanGeneradoEn = datos.PlanGeneradoEn };
            db.ValoracionesViaje.Add(voto);
        }
        voto.FechaUtc = DateTime.UtcNow;
        voto.Util = datos.Util;
        voto.Motivo = datos.Motivo?.Trim();
        voto.Ciudad = datos.Ciudad;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
