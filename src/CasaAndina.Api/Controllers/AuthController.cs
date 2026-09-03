using CasaAndina.Application.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISender _mediator;

    public AuthController(ISender mediator) => _mediator = mediator;

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command)
        => Ok(await _mediator.Send(command));
}
