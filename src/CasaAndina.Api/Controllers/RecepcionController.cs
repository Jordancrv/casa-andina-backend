using CasaAndina.Application.Recepcion;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/recepcion")]
[Authorize]
public sealed class RecepcionController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "recepcion.ver")]
    public async Task<IActionResult> Get([FromQuery] string vista = "llegadas", [FromQuery] DateTime? fecha = null,
        [FromQuery] string? codigo = null, [FromQuery] string? habitacion = null,
        [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 20, CancellationToken cancellationToken = default)
        => Ok(await sender.Send(new GetRecepcionQuery(vista, fecha, codigo, habitacion, pagina, tamanoPagina), cancellationToken));

    [HttpPost("{id:int}/check-in")]
    [Authorize(Policy = "recepcion.ingreso")]
    public async Task<IActionResult> CheckIn(int id, CancellationToken cancellationToken)
    {
        await sender.Send(new RegistrarMovimientoCommand(id, false), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/check-out")]
    [Authorize(Policy = "recepcion.salida")]
    public async Task<IActionResult> CheckOut(int id, CancellationToken cancellationToken)
    {
        await sender.Send(new RegistrarMovimientoCommand(id, true), cancellationToken);
        return NoContent();
    }
}
