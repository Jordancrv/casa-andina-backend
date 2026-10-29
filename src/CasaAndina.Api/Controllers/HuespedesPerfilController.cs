using CasaAndina.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Api.Controllers;

[ApiController, Route("api/perfil/huespedes"), Authorize(Policy = "Cliente")]
public sealed class HuespedesPerfilController(IApplicationDbContext db, ICurrentUserService user) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (user.ClienteId is not int id) return Unauthorized();
        var clienteActivo = await db.Clientes.AnyAsync(c => c.Id == id && c.Estado == "Activo", ct);
        if (!clienteActivo) return NotFound();
        var rows = await db.ClientesHuespedes.AsNoTracking().Where(x => x.ClienteId == id).OrderBy(x => x.Nombres).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Nombres, x.Apellidos, x.Dni, x.FechaNacimiento, x.Sexo, x.Correo, x.Telefono }).ToListAsync(ct);
        Response.Headers.CacheControl = "no-store, private";
        return Ok(rows);
    }
}
