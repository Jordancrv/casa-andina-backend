using CasaAndina.Application.Sedes.DTOs;
using CasaAndina.Application.Sedes.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

/// <summary>
/// Catálogo de sedes de solo lectura. Las altas y modificaciones se realizan
/// exclusivamente mediante scripts SQL controlados.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class SedesController : ControllerBase
{
    private readonly ISender _sender;

    public SedesController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SedeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SedeDto>>> Get(
        [FromQuery] string? region,
        [FromQuery] string? ciudad,
        CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetSedesQuery(region, ciudad), cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SedeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SedeDto>> GetById(
        int id,
        CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetSedeByIdQuery(id), cancellationToken));
}
