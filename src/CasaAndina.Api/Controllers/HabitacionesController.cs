using CasaAndina.Application.Habitaciones.Commands;
using CasaAndina.Application.Habitaciones.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "PersonalInterno")]
public class HabitacionesController : ControllerBase
{
    private readonly ISender _mediator;

    public HabitacionesController(ISender mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetHabitacionesQuery query)
        => Ok(await _mediator.Send(query));

    [HttpPost]
    [Authorize(Policy = "GestionHabitaciones")]
    public async Task<IActionResult> Create(CreateHabitacionCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(Get), new { id }, id);
    }
}
