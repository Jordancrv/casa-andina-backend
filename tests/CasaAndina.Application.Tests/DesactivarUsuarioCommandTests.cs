using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Usuarios.Commands;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class DesactivarUsuarioCommandTests
{
    [Fact]
    public async Task Desactivar_usuario_cambia_estado_y_registra_auditoria_sin_borrarlo()
    {
        await using var context = CreateContext();
        var persona = new Persona
        {
            Id = 10,
            TipoPersona = "N",
            Natural = new PersonaNatural { PersonaId = 10, Nombres = "Cuenta", Apellidos = "Prueba" }
        };
        context.Usuarios.Add(new Usuario { Id = 20, PersonaId = persona.Id, Persona = persona, PasswordHash = "hash-de-prueba" });
        await context.SaveChangesAsync();
        var handler = new DesactivarUsuarioCommandHandler(context, new CurrentUserStub());

        await handler.Handle(new DesactivarUsuarioCommand(20), CancellationToken.None);

        context.Usuarios.Should().ContainSingle(user => user.Id == 20 && !user.Activo);
        context.Auditoria.Should().ContainSingle(entry => entry.Entidad == "Usuario" && entry.Accion == "UPDATE");
    }

    [Fact]
    public async Task Desactivar_usuario_rechaza_auto_desactivacion()
    {
        await using var context = CreateContext();
        var handler = new DesactivarUsuarioCommandHandler(context, new CurrentUserStub(usuarioId: 20));

        var action = () => handler.Handle(new DesactivarUsuarioCommand(20), CancellationToken.None);

        await action.Should().ThrowAsync<CasaAndina.Application.Common.Exceptions.ValidationException>();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private sealed class CurrentUserStub(int? usuarioId = 1) : ICurrentUserService
    {
        public int? UsuarioId => usuarioId;
        public int? ClienteId => null;
        public int? SedeId => null;
        public RolUsuario? Rol => RolUsuario.Administrador;
    }
}
