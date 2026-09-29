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

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await _mediator.Send(new GetHabitacionByIdQuery(id)));

    [HttpPost]
    [Authorize(Policy = "GestionHabitaciones")]
    public async Task<IActionResult> Create(CreateHabitacionCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "GestionHabitaciones")]
    public async Task<IActionResult> Update(int id, UpdateHabitacionCommand command)
    {
        await _mediator.Send(command with { Id = id });
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "GestionHabitaciones")]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _mediator.Send(new DeactivateHabitacionCommand(id));
        return NoContent();
    }
}
