using CasaAndina.Application.Servicios.Commands;
using CasaAndina.Application.Servicios.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServiciosController : ControllerBase
{
    private readonly ISender _sender;

    public ServiciosController(ISender sender) => _sender = sender;

    [HttpGet]
    [Authorize(Policy = "servicios.ver")]
    public async Task<IActionResult> Get([FromQuery] GetServiciosQuery query)
        => Ok(await _sender.Send(query));

    [HttpGet("{id:int}")]
    [Authorize(Policy = "servicios.ver")]
    public async Task<IActionResult> GetById(int id)
        => Ok(await _sender.Send(new GetServicioByIdQuery(id)));

    [HttpPost]
    [Authorize(Policy = "servicios.crear")]
    public async Task<IActionResult> Create(CreateServicioCommand command)
    {
        var id = await _sender.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "servicios.editar")]
    public async Task<IActionResult> Update(int id, UpdateServicioCommand command)
    {
        await _sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "servicios.desactivar")]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _sender.Send(new DeactivateServicioCommand(id));
        return NoContent();
    }
}
