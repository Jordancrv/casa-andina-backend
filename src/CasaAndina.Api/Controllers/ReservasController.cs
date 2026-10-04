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
