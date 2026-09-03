using CasaAndina.Application.Dashboard.Queries;
using CasaAndina.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = nameof(RolUsuario.Administrador))]
public class DashboardController : ControllerBase
{
    private readonly ISender _mediator;

    public DashboardController(ISender mediator) => _mediator = mediator;

    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen()
        => Ok(await _mediator.Send(new GetDashboardResumenQuery()));
}
