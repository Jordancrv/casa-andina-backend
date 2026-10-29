using CasaAndina.Application.Accesos;
using CasaAndina.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Api.Controllers;

[ApiController, Route("api/accesos"), Authorize]
public sealed class AccesosController(IApplicationDbContext db, ICurrentUserService user) : ControllerBase
{
    private RolesService Service => new(db, user);

    [HttpGet("actual")]
    public async Task<IActionResult> Actual(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store, private";
        var roles = await db.UsuarioRoles.AsNoTracking().Where(x => x.UsuarioId == user.UsuarioId && x.Rol.Activo && (x.Rol.SedeId == null || x.Rol.SedeId == user.SedeId))
            .Select(x => x.Rol.Nombre).ToListAsync(ct);
        return Ok(new { user.UsuarioId, user.SedeId, roles, permisos = User.FindAll("permiso").Select(c => c.Value).Distinct() });
    }

    [HttpGet("roles"), Authorize(Policy = "roles.ver")]
    public async Task<IActionResult> Roles(CancellationToken ct) => Ok(await Service.Listar(ct));
    [HttpPost("roles"), Authorize(Policy = "roles.gestionar")]
    public async Task<IActionResult> Crear(GuardarRol input, CancellationToken ct) => Ok(new { id = await Service.Guardar(null, input, ct) });
    [HttpPut("roles/{id:int}"), Authorize(Policy = "roles.gestionar")]
    public async Task<IActionResult> Guardar(int id, GuardarRol input, CancellationToken ct) => Ok(new { id = await Service.Guardar(id, input, ct) });
    [HttpDelete("roles/{id:int}"), Authorize(Policy = "roles.gestionar")]
    public async Task<IActionResult> Desactivar(int id, [FromQuery] Guid version, CancellationToken ct) { await Service.Desactivar(id, version, ct); return NoContent(); }
    [HttpGet("personal"), Authorize(Policy = "usuarios.asignar")]
    public async Task<IActionResult> Personal([FromQuery] string? buscar, [FromQuery] int pagina = 1, CancellationToken ct = default) => Ok(await Service.Personal(buscar, pagina, ct));
    [HttpGet("personal/{id:int}/roles"), Authorize(Policy = "usuarios.asignar")]
    public async Task<IActionResult> RolesPersonal(int id, CancellationToken ct) => Ok(await Service.RolesPersonal(id, ct));
    [HttpPost("personal"), Authorize(Policy = "usuarios.asignar"), Authorize(Policy = "usuarios.editar")]
    public async Task<IActionResult> CrearPersonal(CrearPersonal input, [FromServices] IPasswordHasher hasher, CancellationToken ct) => Ok(new { id = await Service.CrearPersonalAsync(input, hasher, ct) });
    [HttpPut("personal/{id:int}/roles"), Authorize(Policy = "usuarios.asignar")]
    public async Task<IActionResult> Asignar(int id, AsignarRoles input, CancellationToken ct) { await Service.Asignar(id, input, ct); return NoContent(); }
}
