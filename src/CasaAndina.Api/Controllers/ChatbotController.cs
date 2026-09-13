using CasaAndina.Application.Chatbot.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

/// <summary>RF05/RF06: expuesto al Portal Cliente (requiere JWT de cliente).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatbotController : ControllerBase

{
    private readonly ISender _mediator;

    public ChatbotController(ISender mediator) => _mediator = mediator;

    [HttpGet("recomendaciones")]
    public async Task<IActionResult> GetRecomendaciones([FromQuery] GetRecomendacionesQuery query)
        => Ok(await _mediator.Send(query));
}
