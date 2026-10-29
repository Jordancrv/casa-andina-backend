using CasaAndina.Application.Comodidades;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/comodidades")]
[Authorize]
public sealed class ComodidadesController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "comodidades.crear")]
    public async Task<IActionResult> Create(CrearComodidadCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));
}
