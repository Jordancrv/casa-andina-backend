using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Usuarios.Commands;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ActivarUsuarioCommandTests
{
    private sealed class Actor(params string[] permisos) : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
        public int? SedeId => 5;
        public RolUsuario? Rol => RolUsuario.Administrador;
        public bool TienePermiso(string permiso) => permisos.Contains(permiso);
    }
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Usuario Personal(int id, int sede = 5) => new() { Id = id, SedeId = sede, Activo = false, Correo = $"{id}@example.test", PasswordHash = "hash", Persona = new Persona { Id = id, Natural = new PersonaNatural { Nombres = "Personal" } } };

    [Fact]
    public async Task Activa_cuenta_y_audita_una_sola_vez_sin_cambiar_roles()
    {
        await using var db = Context(); var user = Personal(2);
        var permission = new Permiso { Codigo = "recepcion.ver", Seccion = "recepcion", Delegable = true };
        var role = new Rol { Id = 2, Codigo = "RECEPCION", Nombre = "Recepción", Permisos = { new RolPermiso { Permiso = permission, PermisoCodigo = permission.Codigo } } };
        user.Roles.Add(new UsuarioRol { Rol = role }); db.Usuarios.Add(user); await db.SaveChangesAsync();
        var handler = new ActivarUsuarioCommandHandler(db, new Actor("usuarios.desactivar", "recepcion.ver"));
        await handler.Handle(new(2), default); await handler.Handle(new(2), default);
        user.Activo.Should().BeTrue(); user.Roles.Should().ContainSingle();
        db.Auditoria.Should().ContainSingle(a => a.Accion == "UPDATE" && a.Antes == "{\"estado\":\"Inactivo\"}" && a.Despues == "{\"estado\":\"Activo\"}");
    }
    [Fact]
    public async Task Rechaza_otra_sede_cliente_administrador_y_falta_de_permiso()
    {
        await using var db = Context();
        var other = Personal(2, 6); var client = Personal(3); client.Persona.Cliente = new Cliente();
        var admin = Personal(4); admin.Roles.Add(new UsuarioRol { Rol = new Rol { Id = 4, Codigo = "ADMINISTRADOR", Nombre = "Administrador" } });
        db.Usuarios.AddRange(other, client, admin); await db.SaveChangesAsync();
        var handler = new ActivarUsuarioCommandHandler(db, new Actor("usuarios.desactivar"));
        await FluentActions.Invoking(() => handler.Handle(new(2), default)).Should().ThrowAsync<NotFoundException>();
        await FluentActions.Invoking(() => handler.Handle(new(3), default)).Should().ThrowAsync<NotFoundException>();
        await FluentActions.Invoking(() => handler.Handle(new(4), default)).Should().ThrowAsync<ForbiddenAccessException>();
        await FluentActions.Invoking(() => new ActivarUsuarioCommandHandler(db, new Actor()).Handle(new(2), default)).Should().ThrowAsync<ForbiddenAccessException>();
        db.Auditoria.Should().BeEmpty();
    }
    [Fact]
    public async Task Rechaza_reactivar_roles_con_permisos_fuera_del_alcance()
    {
        await using var db = Context(); var user = Personal(2);
        var permission = new Permiso { Codigo = "reportes.ver", Seccion = "reportes", Delegable = true };
        user.Roles.Add(new UsuarioRol { Rol = new Rol { Id = 2, Codigo = "LOCAL", Nombre = "Local", Permisos = { new RolPermiso { Permiso = permission, PermisoCodigo = permission.Codigo } } } });
        db.Usuarios.Add(user); await db.SaveChangesAsync();
        await FluentActions.Invoking(() => new ActivarUsuarioCommandHandler(db, new Actor("usuarios.desactivar")).Handle(new(2), default)).Should().ThrowAsync<ForbiddenAccessException>();
        user.Activo.Should().BeFalse(); db.Auditoria.Should().BeEmpty();
    }
}
