using CasaAndina.Application.Reservas.Queries;
using CasaAndina.Application.Reservas.Commands;
using CasaAndina.Application.Reservas.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

/// <summary>RF03: lista las reservas del cliente autenticado (Portal Cliente).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Cliente")]
public class ReservasController : ControllerBase
{
    private readonly ISender _mediator;

    public ReservasController(ISender mediator) => _mediator = mediator;

    /// <summary>
    /// GET /api/reservas — devuelve todas las reservas del usuario autenticado,
    /// ordenadas de la más reciente a la más antigua.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMisReservas()
        => Ok(await _mediator.Send(new GetReservasQuery()));

    [HttpGet("{id:int}/detalle")]
    public async Task<IActionResult> GetDetalle(int id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store, private";
        return Ok(await _mediator.Send(new GetReservaDetalleQuery(id), cancellationToken));
    }

    [HttpPost("{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new CancelarReservaCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Genera el PDF del comprobante propio, solo si el pago aprobado cubre el total.</summary>
    [HttpPost("{id:int}/comprobante/pdf")]
    public async Task<IActionResult> ComprobantePdf(int id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GenerarComprobantePdfCommand(id), cancellationToken);
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Contenido, "application/pdf", result.NombreArchivo);
    }

    [HttpGet("metodos-pago")]
    public async Task<ActionResult<IReadOnlyList<MetodoPagoDto>>> GetMetodosPago(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetMetodosPagoQuery(), cancellationToken));

    /// <summary>GET /api/reservas/disponibilidad?SedeId=1&amp;FechaCheckIn=...&amp;FechaCheckOut=...</summary>
    [AllowAnonymous]
    [HttpGet("disponibilidad")]
    public async Task<ActionResult<IReadOnlyList<HabitacionDisponibleDto>>> GetDisponibilidad(
        [FromQuery] int sedeId,
        [FromQuery] DateTime fechaCheckIn,
        [FromQuery] DateTime fechaCheckOut,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetDisponibilidadQuery(sedeId, fechaCheckIn, fechaCheckOut), cancellationToken));

    [AllowAnonymous]
    [HttpGet("servicios")]
    public async Task<IActionResult> GetServicios([FromQuery] int sedeId, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetServiciosReservaQuery(sedeId), cancellationToken));

    /// <summary>POST /api/reservas — crea la compra autenticada y simula el pago sin cobrar.</summary>
    [HttpPost]
    public async Task<ActionResult<ReservaCompraResponseDto>> Comprar(
        [FromBody] CrearReservaCompraCommand command,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetMisReservas), new { id = response.ReservaId }, response);
    }
}
