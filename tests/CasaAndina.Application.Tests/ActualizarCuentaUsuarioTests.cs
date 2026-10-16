using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Usuarios.Commands;
using CasaAndina.Application.Usuarios.DTOs;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ActualizarCuentaUsuarioTests
{
    [Fact]
    public async Task ActualizaCorreoDeAccesoSinCambiarCorreoDeNotificacion()
    {
        await using var context = CreateContext();
        var persona = new Persona
        {
            Id = 2,
            TipoPersona = "N",
            Natural = new PersonaNatural { PersonaId = 2, Nombres = "Cliente" },
            Contactos =
            {
                new PersonaContacto { TipoContacto = "Correo", Valor = "notificaciones@example.test", EsPrincipal = true },
                new PersonaContacto { TipoContacto = "Telefono", Valor = "+51987654321", EsPrincipal = true }
            }
        };
        context.Usuarios.Add(new Usuario
        {
            Id = 2,
            PersonaId = persona.Id,
            Persona = persona,
            Correo = "anterior@example.test",
            SedeId = 5,
            PasswordHash = "hash"
        });
        await context.SaveChangesAsync();
        var handler = new ActualizarCuentaUsuarioCommandHandler(context, new CurrentUserStub());

        await handler.Handle(new ActualizarCuentaUsuarioCommand(2, " NUEVO@example.test ", "+51999999999"), CancellationToken.None);

        (await context.Usuarios.SingleAsync(user => user.Id == 2)).Correo.Should().Be("nuevo@example.test");
        (await context.PersonaContactos.SingleAsync(contact => contact.TipoContacto == "Correo"))
            .Valor.Should().Be("notificaciones@example.test");
        (await context.PersonaContactos.SingleAsync(contact => contact.TipoContacto == "Telefono"))
            .Valor.Should().Be("+51999999999");
    }

    [Fact]
    public async Task RechazaCorreoDeAccesoYaAsignadoAOtroUsuario()
    {
        await using var context = CreateContext();
        var firstPerson = new Persona { Id = 1, TipoPersona = "N", Natural = new PersonaNatural { PersonaId = 1, Nombres = "Uno" } };
        var secondPerson = new Persona { Id = 2, TipoPersona = "N", Natural = new PersonaNatural { PersonaId = 2, Nombres = "Dos" } };
        context.Usuarios.AddRange(
            new Usuario { Id = 1, Correo = "admin@example.test", PersonaId = 1, Persona = firstPerson, SedeId = 5, PasswordHash = "hash" },
            new Usuario { Id = 2, Correo = "ocupado@example.test", PersonaId = 2, Persona = secondPerson, SedeId = 5, PasswordHash = "hash" });
        await context.SaveChangesAsync();
        var handler = new ActualizarCuentaUsuarioCommandHandler(context, new CurrentUserStub());

        var action = () => handler.Handle(
            new ActualizarCuentaUsuarioCommand(1, "OCUPADO@example.test", null), CancellationToken.None);

        await action.Should().ThrowAsync<CasaAndina.Application.Common.Exceptions.ValidationException>();
        (await context.Usuarios.SingleAsync(user => user.Id == 1)).Correo.Should().Be("admin@example.test");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private sealed class CurrentUserStub : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
        public int? SedeId => 5;
        public RolUsuario? Rol => RolUsuario.Administrador;
    }
}
