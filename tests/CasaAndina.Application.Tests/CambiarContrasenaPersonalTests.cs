using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Usuarios.Commands;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class CambiarContrasenaPersonalTests
{
    private sealed record Actor(RolUsuario? Rol = RolUsuario.Administrador, int? SedeId = 5, int? UsuarioId = 1, int? ClienteId = null, bool Permitido = true) : ICurrentUserService
    { public bool TienePermiso(string permiso) => Permitido && permiso == "usuarios.editar"; }
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Usuario Personal(int id, int sede = 5) => new() { Id = id, SedeId = sede, Activo = true, Correo = $"{id}@example.test", PasswordHash = "old", Persona = new Persona { Id = id, Natural = new PersonaNatural { Nombres = "Personal" } } };

    [Fact]
    public async Task Administrador_actualiza_hash_y_audita_sin_guardar_la_clave()
    {
        await using var db = Context(); var user = Personal(2); db.Usuarios.Add(user); await db.SaveChangesAsync();
        var hasher = new PasswordHasher(); const string password = "PruebaSegura123!";
        await new CambiarContrasenaPersonalCommandHandler(db, new Actor(), hasher).Handle(new(2, password), default);
        user.PasswordHash.Should().NotBe(password); hasher.Verify(password, user.PasswordHash).Should().BeTrue();
        var audit = await db.Auditoria.SingleAsync(); audit.UsuarioId.Should().Be(1); audit.SedeId.Should().Be(5);
        audit.Despues.Should().Contain("CambiarContrasenaPersonal").And.NotContain(password).And.NotContain(user.PasswordHash);
    }

    [Fact]
    public async Task No_permite_otra_sede_cliente_administrador_ni_usuarios_sin_autorizacion()
    {
        await using var db = Context(); var other = Personal(2, 6); var client = Personal(3); client.Persona.Cliente = new Cliente();
        var admin = Personal(4); admin.Roles.Add(new UsuarioRol { Rol = new Rol { Id = 4, Codigo = "ADMINISTRADOR", Nombre = "Administrador" } });
        db.Usuarios.AddRange(other, client, admin); await db.SaveChangesAsync(); var hash = new PasswordHasher();
        var handler = new CambiarContrasenaPersonalCommandHandler(db, new Actor(), hash);
        await FluentActions.Invoking(() => handler.Handle(new(2, "PruebaSegura123!"), default)).Should().ThrowAsync<NotFoundException>();
        await FluentActions.Invoking(() => handler.Handle(new(3, "PruebaSegura123!"), default)).Should().ThrowAsync<NotFoundException>();
        await FluentActions.Invoking(() => handler.Handle(new(4, "PruebaSegura123!"), default)).Should().ThrowAsync<ForbiddenAccessException>();
        await FluentActions.Invoking(() => new CambiarContrasenaPersonalCommandHandler(db, new Actor(Rol: RolUsuario.Recepcion), hash).Handle(new(4, "PruebaSegura123!"), default)).Should().ThrowAsync<ForbiddenAccessException>();
        await FluentActions.Invoking(() => new CambiarContrasenaPersonalCommandHandler(db, new Actor(Permitido: false), hash).Handle(new(4, "PruebaSegura123!"), default)).Should().ThrowAsync<ForbiddenAccessException>();
        db.Auditoria.Should().BeEmpty();
    }

    [Theory]
    [InlineData("corta")]
    [InlineData("sinmayuscula123!")]
    [InlineData("SINMINUSCULA123!")]
    [InlineData("SinNumeros!!")]
    [InlineData("SinSimbolos123")]
    public void Valida_politica_de_contrasena(string password)
        => new CambiarContrasenaPersonalCommandValidator().Validate(new CambiarContrasenaPersonalCommand(2, password)).IsValid.Should().BeFalse();
}
