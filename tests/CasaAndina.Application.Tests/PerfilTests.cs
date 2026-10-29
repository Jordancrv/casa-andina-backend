using CasaAndina.Api.Controllers;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class PerfilTests
{
    private sealed record User(int? UsuarioId) : ICurrentUserService
    { public int? ClienteId => null; public int? SedeId => 1; public RolUsuario? Rol => RolUsuario.Administrador; }
    private sealed record ClienteUser(int? ClienteId) : ICurrentUserService
    { public int? UsuarioId => 1; public int? SedeId => null; public RolUsuario? Rol => RolUsuario.Cliente; }

    [Fact]
    public async Task Huespedes_guardados_solo_se_consultan_por_cliente_autenticado()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Clientes.AddRange(new Cliente { Id = 1, Estado = "Activo" }, new Cliente { Id = 2, Estado = "Activo" });
        db.ClientesHuespedes.AddRange(new ClienteHuesped { ClienteId = 1, HuespedId = 1, Nombres = "Propio", Apellidos = "Persona", Dni = "12345678", Sexo = "F", Correo = "propio@test.example" }, new ClienteHuesped { ClienteId = 2, HuespedId = 2, Nombres = "Ajeno", Apellidos = "Persona", Dni = "87654321", Sexo = "M", Correo = "ajeno@test.example" });
        await db.SaveChangesAsync();
        var controller = new HuespedesPerfilController(db, new ClienteUser(1)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var result = (await controller.Get(default)).Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        json.Should().Contain("Propio").And.NotContain("Ajeno").And.NotContain("87654321");
        (await new HuespedesPerfilController(db, new ClienteUser(null)).Get(default)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Perfil_solo_devuelve_cuenta_actual_y_no_expone_contrasena()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Usuarios.AddRange(
            new Usuario { Id = 1, Correo = "uno@test.example", PasswordHash = "secreto", PersonaId = 1, Persona = new Persona { Id = 1, TipoPersona = "N", Natural = new PersonaNatural { PersonaId = 1, Nombres = "Ana" } } },
            new Usuario { Id = 2, Correo = "dos@test.example", PasswordHash = "otro", PersonaId = 2, Persona = new Persona { Id = 2, TipoPersona = "N", Natural = new PersonaNatural { PersonaId = 2, Nombres = "Luis" } } });
        await db.SaveChangesAsync();
        var controller = new PerfilController(db, new User(1)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var result = (await controller.Get(default)).Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        json.Should().Contain("uno@test.example").And.NotContain("dos@test.example").And.NotContain("secreto");
        controller.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
        var noUser = new PerfilController(db, new User(null));
        (await noUser.Get(default)).Should().BeOfType<UnauthorizedResult>();
    }
}
