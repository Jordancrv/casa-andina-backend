using CasaAndina.Application.Usuarios.Commands;
using CasaAndina.Application.Usuarios.DTOs;
using CasaAndina.Application.Usuarios.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Administrador")]
public sealed class UsuariosController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? buscar,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await sender.Send(new GetUsuariosAdminQuery(buscar, pageNumber, pageSize), cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetUsuarioAdminByIdQuery(id), cancellationToken));

    [HttpPut("{id:int}/cuenta")]
    public async Task<IActionResult> UpdateAccount(
        int id,
        [FromBody] ActualizarCuentaUsuarioCommand command,
        CancellationToken cancellationToken)
    {
        await sender.Send(command with { UsuarioId = id }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await sender.Send(new DesactivarUsuarioCommand(id), cancellationToken);
        return NoContent();
    }
}
