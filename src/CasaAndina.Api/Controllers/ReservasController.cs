using CasaAndina.Application.Reservas.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

/// <summary>RF03: lista las reservas del cliente autenticado (Portal Cliente).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
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
}
