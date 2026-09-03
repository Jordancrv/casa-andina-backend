using CasaAndina.Application.Chatbot.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

/// <summary>RF05/RF06: expuesto al Portal Cliente (login de Cliente requerido en el guard del frontend).</summary>
[ApiController]
[Route("api/[controller]")]
public class ChatbotController : ControllerBase
{
    private readonly ISender _mediator;

    public ChatbotController(ISender mediator) => _mediator = mediator;

    [HttpGet("recomendaciones")]
    public async Task<IActionResult> GetRecomendaciones([FromQuery] GetRecomendacionesQuery query)
        => Ok(await _mediator.Send(query));
}
