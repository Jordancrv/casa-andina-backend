using CasaAndina.Application.Dashboard.Queries;
using CasaAndina.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ISender _mediator;

    public DashboardController(ISender mediator) => _mediator = mediator;

    [HttpGet("resumen")]
    [Authorize(Policy = "dashboard.ver")]
    public async Task<IActionResult> GetResumen()
        => Ok(await _mediator.Send(new GetDashboardResumenQuery()));

    [HttpGet("bi")]
    [Authorize(Policy = "indicadores.consultar")]
    public async Task<IActionResult> GetBi([FromQuery] string periodo = "30d")
        => Ok(await _mediator.Send(new GetDashboardBiQuery(periodo)));

    [HttpGet("proyeccion-ocupacion")]
    [Authorize(Policy = "dashboard.ver")]
    public async Task<IActionResult> GetProyeccionOcupacion(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetProyeccionOcupacionQuery(), cancellationToken));

    [HttpGet("reporte/pdf")]
    [Produces("application/pdf")]
    [Authorize(Policy = "reportes.exportar")]
    public async Task<IActionResult> GetReporte([FromQuery] string periodo = "30d", CancellationToken cancellationToken = default)
    {
        var reporte = await _mediator.Send(new GetDashboardReportQuery(periodo), cancellationToken);
        Response.Headers.CacheControl = "no-store, private";
        return File(reporte.Contenido, "application/pdf", reporte.Nombre);
    }
}
