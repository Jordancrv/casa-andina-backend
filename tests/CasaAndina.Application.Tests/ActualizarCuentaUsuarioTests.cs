using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Usuarios.Commands;
using CasaAndina.Application.Usuarios.DTOs;
using CasaAndina.Application.Usuarios.Queries;
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

    [Fact]
    public async Task Rechaza_editar_cuenta_de_otra_sede_aunque_el_cliente_tenga_reserva_local()
    {
        await using var context = CreateContext();
        await SeedCrossSiteCustomer(context);
        var handler = new ActualizarCuentaUsuarioCommandHandler(context, new CurrentUserStub());

        var action = () => handler.Handle(
            new ActualizarCuentaUsuarioCommand(2, "cambio@example.test", null), CancellationToken.None);

        await action.Should().ThrowAsync<CasaAndina.Application.Common.Exceptions.NotFoundException>();
        (await context.Usuarios.SingleAsync(user => user.Id == 2)).Correo.Should().Be("cliente@example.test");
    }

    [Fact]
    public async Task Lista_usuarios_no_expone_cuentas_asignadas_a_otra_sede()
    {
        await using var context = CreateContext();
        await SeedCrossSiteCustomer(context);
        var handler = new GetUsuariosAdminQueryHandler(context, new CurrentUserStub());

        var result = await handler.Handle(new GetUsuariosAdminQuery(), CancellationToken.None);

        result.Items.Should().NotContain(user => user.UsuarioId == 2);
    }

    internal static async Task SeedCrossSiteCustomer(ApplicationDbContext context)
    {
        var persona = new Persona
        {
            Id = 20,
            TipoPersona = "N",
            Natural = new PersonaNatural { PersonaId = 20, Nombres = "Cliente" }
        };
        var cliente = new Cliente { Id = 20, PersonaId = persona.Id, Persona = persona };
        var sedeLocal = new Sede { Id = 5, Nombre = "Sede local", Codigo = "LOC", Region = "Costa", Ciudad = "Lima", Categoria = "Select" };
        var sedeAjena = new Sede { Id = 6, Nombre = "Otra sede", Codigo = "OTR", Region = "Sierra", Ciudad = "Cusco", Categoria = "Select" };
        context.Sedes.AddRange(sedeLocal, sedeAjena);
        context.Usuarios.Add(new Usuario
        {
            Id = 2,
            Correo = "cliente@example.test",
            PersonaId = persona.Id,
            Persona = persona,
            SedeId = sedeAjena.Id,
            PasswordHash = "hash"
        });
        cliente.Reservas.Add(new Reserva
        {
            Id = 30,
            CodigoReserva = "CA-LOCAL",
            ClienteId = cliente.Id,
            Cliente = cliente,
            SedeId = sedeLocal.Id,
            Sede = sedeLocal,
            PrecioTotal = 100
        });
        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();
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
