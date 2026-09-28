using CasaAndina.Application.Auth.Commands;
using CasaAndina.Application.Auth.DTOs;
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
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginCommand command,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command, cancellationToken));
}
