using CasaAndina.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CasaAndina.Application.Perfil;
using Microsoft.AspNetCore.RateLimiting;

namespace CasaAndina.Api.Controllers;

[ApiController, Route("api/perfil"), Authorize]
public sealed class PerfilController(IApplicationDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (currentUser.UsuarioId is not int id) return Unauthorized();
        var perfil = await db.Usuarios.AsNoTracking().Where(u => u.Id == id && u.Activo)
            .Select(u => new {
                u.Correo, u.FechaCreacion, u.UltimoAcceso, u.SedeId,
                Sede = u.Sede != null ? u.Sede.Nombre : null,
                TipoCuenta = u.Persona.Cliente != null ? "Cliente" : "Personal",
                TipoPersona = u.Persona.TipoPersona,
                Nombres = u.Persona.Natural != null ? u.Persona.Natural.Nombres : null,
                Apellidos = u.Persona.Natural != null ? u.Persona.Natural.Apellidos : null,
                FechaNacimiento = u.Persona.Natural != null ? u.Persona.Natural.FechaNacimiento : null,
                Genero = u.Persona.Natural != null ? u.Persona.Natural.Genero : null,
                Nacionalidad = u.Persona.Natural != null ? u.Persona.Natural.Nacionalidad : null,
                RazonSocial = u.Persona.Juridica != null ? u.Persona.Juridica.RazonSocial : null,
                NombreComercial = u.Persona.Juridica != null ? u.Persona.Juridica.NombreComercial : null,
                Documentos = u.Persona.Documentos.Select(d => new { Tipo = d.TipoDocumento.Nombre, Codigo = d.TipoDocumento.Codigo, d.Numero, d.EsPrincipal }).ToArray(),
                Contactos = u.Persona.Contactos.Select(c => new { c.TipoContacto, c.Valor, c.EsPrincipal }).ToArray(),
                Roles = u.Roles.Where(r => r.Rol.Activo && (r.Rol.SedeId == null || r.Rol.SedeId == u.SedeId)).Select(r => r.Rol.Nombre).ToArray()
            }).SingleOrDefaultAsync(ct);
        Response.Headers.CacheControl = "no-store, private";
        return perfil is null ? NotFound() : Ok(perfil);
    }

    [HttpPost("correos"), Authorize(Policy = "Cliente")]
    public async Task<IActionResult> AgregarCorreo(CorreoPerfilRequest request, CancellationToken ct)
    { await new CorreosPerfilService(db, currentUser).Agregar(request.Correo, ct); return NoContent(); }

    [HttpPut("correo-principal"), Authorize(Policy = "Cliente")]
    public async Task<IActionResult> CambiarPrincipal(CorreoPerfilRequest request, CancellationToken ct)
    { await new CorreosPerfilService(db, currentUser).Principal(request.Correo, ct); return NoContent(); }

    [HttpPut("correo-acceso"), Authorize(Policy = "Cliente"), EnableRateLimiting("login")]
    public async Task<IActionResult> CambiarAcceso(CorreoAccesoRequest request, [FromServices] IPasswordHasher hasher, CancellationToken ct)
    { await new CorreosPerfilService(db, currentUser).Acceso(request, hasher, ct); return NoContent(); }
}
