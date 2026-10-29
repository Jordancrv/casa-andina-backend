using CasaAndina.Application.Clientes.Queries;
using CasaAndina.Application.Clientes.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ClientesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "clientes.ver")]
    public async Task<IActionResult> Get([FromQuery] GetClientesAdminQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    [HttpGet("catalogos")]
    [Authorize(Policy = "clientes.ver")]
    public async Task<IActionResult> Catalogos(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCatalogosCrmQuery(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "clientes.crear")]
    public async Task<IActionResult> Crear([FromBody] ClienteCrmDatos datos, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, new { clienteId = await sender.Send(new CrearClienteCommand(datos), cancellationToken) });

    [HttpPut("{id:int}")]
    [Authorize(Policy = "clientes.editar")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ClienteCrmDatos datos, CancellationToken cancellationToken)
    {
        await sender.Send(new ActualizarClienteCommand(id, datos), cancellationToken);
        return NoContent();
    }
}
