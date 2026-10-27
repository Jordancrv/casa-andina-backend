using CasaAndina.Application.Chatbot.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CasaAndina.Api.Controllers;

/// <summary>RF05/RF06: expuesto al Portal Cliente (requiere JWT de cliente).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Cliente")]
public class ChatbotController : ControllerBase

{
    private readonly ISender _mediator;

    public ChatbotController(ISender mediator) => _mediator = mediator;

    [HttpGet("recomendaciones")]
    public async Task<IActionResult> GetRecomendaciones([FromQuery] GetRecomendacionesQuery query, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [HttpPost("planificar")]
    [EnableRateLimiting("asistente")]
    public async Task<IActionResult> Planificar([FromBody] PlanificarViajeQuery query, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [HttpPost("conversar")]
    [EnableRateLimiting("asistente")]
    public async Task<IActionResult> Conversar([FromBody] ConversarViajeQuery query, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));
}
