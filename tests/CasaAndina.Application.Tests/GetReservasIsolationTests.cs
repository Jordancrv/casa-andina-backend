using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class GetReservasIsolationTests
{
    [Fact]
    public async Task MisReservas_DeberiaDevolverSoloReservasDelClienteAutenticado()
    {
        await using var context = CreateContext();
        SeedReservations(context);
        var handler = new GetReservasQueryHandler(context, new CurrentUserStub(clienteId: 10));

        var result = await handler.Handle(new GetReservasQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].ReservaId.Should().Be(100);
        result[0].Codigo.Should().Be("CA-CLIENTE-10");
    }

    [Fact]
    public async Task MisReservas_DeberiaRechazarContextoSinClienteAutenticado()
    {
        await using var context = CreateContext();
        var handler = new GetReservasQueryHandler(context, new CurrentUserStub(clienteId: null));

        var action = () => handler.Handle(new GetReservasQuery(), CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedReservations(ApplicationDbContext context)
    {
        var type = new TipoHabitacion { Id = 1, Nombre = "Doble" };
        var hotel = new Sede
        {
            Id = 1, Nombre = "Sede de prueba", Codigo = "TST", Region = "Sierra",
            Ciudad = "Cusco", Categoria = "Standard"
        };
        var room = new Habitacion
        {
            Id = 20, SedeId = hotel.Id, Sede = hotel, TipoHabitacionId = type.Id,
            TipoHabitacion = type, Numero = "201", Piso = 2, PrecioNoche = 100
        };
        var first = CreateClient(10, 100, "CA-CLIENTE-10", hotel, room);
        var second = CreateClient(11, 101, "CA-CLIENTE-11", hotel, room);

        context.Reservas.AddRange(first, second);
        context.SaveChanges();
    }

    private static Reserva CreateClient(int clientId, int reservationId, string code, Sede hotel, Habitacion room)
    {
        var persona = new Persona
        {
            Id = clientId + 1000,
            Natural = new PersonaNatural { Nombres = "Cliente", Apellidos = clientId.ToString() }
        };
        var client = new Cliente { Id = clientId, PersonaId = persona.Id, Persona = persona };
        var reservation = new Reserva
        {
            Id = reservationId,
            CodigoReserva = code,
            ClienteId = clientId,
            Cliente = client,
            SedeId = hotel.Id,
            Sede = hotel,
            PrecioTotal = 100
        };
        reservation.Habitaciones.Add(new ReservaHabitacion
        {
            Id = reservationId + 1000,
            ReservaId = reservationId,
            Reserva = reservation,
            HabitacionId = room.Id,
            Habitacion = room,
            FechaCheckIn = new DateTime(2026, 12, 1),
            FechaCheckOut = new DateTime(2026, 12, 2),
            CantidadPersonas = 1,
            PrecioAcordado = 100,
            Subtotal = 100
        });
        return reservation;
    }

    private sealed class CurrentUserStub(int? clienteId) : ICurrentUserService
    {
        public int? UsuarioId => clienteId;
        public int? ClienteId => clienteId;
        public int? SedeId => null;
        public RolUsuario? Rol => clienteId.HasValue ? RolUsuario.Cliente : null;
    }
}
