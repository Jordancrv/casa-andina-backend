using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common;
using CasaAndina.Application.Reservas.Commands;
using CasaAndina.Application.Reservas.DTOs;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class CrearReservaCompraHandlerTests
{
    [Fact]
    public async Task Titular_que_usa_su_correo_de_acceso_se_reutiliza_como_huesped_sin_duplicar_persona()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var handler = new CrearReservaCompraCommandHandler(context, new CurrentUserStub(seed.ClienteId, seed.UsuarioId));

        var result = await handler.Handle(Command(" TITULAR@example.test ", true), CancellationToken.None);

        context.Personas.Count().Should().Be(1);
        var guest = await context.Huespedes.SingleAsync();
        guest.PersonaId.Should().Be(seed.PersonaId);
        (await context.PersonaContactos.CountAsync()).Should().Be(0, "the login email is not copied to notification contacts");
        (await context.AsignacionesHuespedes.SingleAsync()).HuespedId.Should().Be(guest.Id);
        result.Estado.Should().Be("Confirmada");
    }

    [Fact]
    public async Task Huesped_nuevo_se_registra_como_persona_natural_y_se_asigna_a_la_reserva()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var handler = new CrearReservaCompraCommandHandler(context, new CurrentUserStub(seed.ClienteId, seed.UsuarioId));

        await handler.Handle(Command("visita@example.test", false), CancellationToken.None);

        context.Personas.Count().Should().Be(2);
        var guest = await context.Huespedes.Include(item => item.PersonaNatural)
            .SingleAsync(item => item.PersonaNatural.Nombres == "María José");
        guest.PersonaNatural.Nombres.Should().Be("María José");
        guest.PersonaNatural.Apellidos.Should().Be("O’Neil");
        (await context.PersonaContactos.SingleAsync()).Valor.Should().Be("visita@example.test");
        (await context.AsignacionesHuespedes.CountAsync()).Should().Be(2);
        (await context.AsignacionesHuespedes.SingleAsync(item => item.HuespedId == guest.Id)).Should().NotBeNull();
    }

    private static CrearReservaCompraCommand Command(string email, bool titular)
    {
        var guests = titular
            ? new[] { new HuespedCompraDto("María José", "O’Neil", email, null, true) }
            : [
                new HuespedCompraDto("Titular", "Prueba", "titular@example.test", null, true),
                new HuespedCompraDto("María José", "O’Neil", email, null, false)
            ];
        return new CrearReservaCompraCommand(
            1,
            PeruBusinessTime.Today.AddDays(2),
            PeruBusinessTime.Today.AddDays(4),
            [new HabitacionCompraDto(1, guests)],
            [],
            "Boleta",
            null,
            null,
            "YAPE");
    }

    private static async Task<SeedResult> SeedAsync(ApplicationDbContext context)
    {
        var site = new Sede
        {
            Nombre = "Sede de prueba", Codigo = "TST", Region = "Sierra", Ciudad = "Cusco", Categoria = "Standard"
        };
        var type = new TipoHabitacion { Nombre = "Doble", CapacidadAdultos = 2, CapacidadNinos = 0 };
        var room = new Habitacion
        {
            Sede = site, TipoHabitacion = type, Numero = "101", Piso = 1,
            PrecioNoche = 100m, Estado = "Disponible", Activo = true
        };
        var person = new Persona
        {
            TipoPersona = "N", Estado = "Activo", FechaRegistro = PeruBusinessTime.Today,
            Natural = new PersonaNatural { Nombres = "Titular", Apellidos = "Prueba" }
        };
        var client = new Cliente { Persona = person, Estado = "Activo", FechaRegistro = PeruBusinessTime.Today };
        var user = new Usuario { Correo = "titular@example.test", PasswordHash = "test-hash", Persona = person };
        person.Cliente = client;
        person.Usuario = user;

        context.AddRange(
            room,
            client,
            user,
            new MetodoPago { Codigo = "YAPE", Nombre = "Yape", Categoria = "BilleteraDigital", Activo = true },
            new EstadoPago { Codigo = "APROBADO", Nombre = "Aprobado", EsFinal = true, Activo = true });
        await context.SaveChangesAsync();
        return new SeedResult(person.Id, client.Id, user.Id);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed record SeedResult(int PersonaId, int ClienteId, int UsuarioId);

    private sealed class CurrentUserStub(int clienteId, int usuarioId) : ICurrentUserService
    {
        public int? UsuarioId => usuarioId;
        public int? ClienteId => clienteId;
        public int? SedeId => null;
        public RolUsuario? Rol => RolUsuario.Cliente;
    }
}
