using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class CalendarioReservasDataTests
{
    [Theory]
    [InlineData(RolUsuario.Administrador, "Jordan Prueba")]
    [InlineData(RolUsuario.Recepcion, null)]
    [InlineData(RolUsuario.Superadministrador, null)]
    public async Task Calendario_expone_nombre_solo_a_rol_administrador(
        RolUsuario role, string? expectedName)
    {
        await using var context = CreateContext();
        var hotel = new Sede { Id = 7, Nombre = "Sede de prueba", Codigo = "TST", Region = "Sierra", Ciudad = "Cusco", Categoria = "Standard" };
        var tipo = new TipoHabitacion { Id = 3, Nombre = "Doble" };
        var persona = new Persona { Id = 10, TipoPersona = "N", Natural = new PersonaNatural { PersonaId = 10, Nombres = "Jordan", Apellidos = "Prueba" } };
        var client = new Cliente { Id = 20, PersonaId = persona.Id, Persona = persona, Estado = "Activo" };
        var room = new Habitacion { Id = 30, SedeId = hotel.Id, Sede = hotel, TipoHabitacionId = tipo.Id, TipoHabitacion = tipo, Numero = "201", Piso = 2 };
        var booking = new Reserva
        {
            Id = 40, CodigoReserva = "CA-TEST-40", ClienteId = client.Id, Cliente = client,
            SedeId = hotel.Id, Sede = hotel, Estado = "Pendiente", PrecioTotal = 320,
            Habitaciones = { new ReservaHabitacion
            {
                Id = 50, HabitacionId = room.Id, Habitacion = room,
                FechaCheckIn = new DateTime(2026, 12, 1), FechaCheckOut = new DateTime(2026, 12, 3),
                CantidadPersonas = 1, PrecioAcordado = 160, Subtotal = 320
            } }
        };
        context.Reservas.Add(booking);
        await context.SaveChangesAsync();
        var handler = new GetCalendarioReservasQueryHandler(context, new CurrentUserStub(hotel.Id, role));

        var result = await handler.Handle(
            new GetCalendarioReservasQuery(hotel.Id, new DateTime(2026, 12, 1), new DateTime(2026, 12, 5)),
            CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Reservas.Should().ContainSingle();
        result[0].Reservas[0].Estado.Should().Be("Pendiente");
        result[0].Reservas[0].NombreReservante.Should().Be(expectedName);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private sealed class CurrentUserStub(int? siteId, RolUsuario role) : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
        public int? SedeId => siteId;
        public RolUsuario? Rol => role;
    }
}
