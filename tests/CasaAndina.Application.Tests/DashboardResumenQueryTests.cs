using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Dashboard.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class DashboardResumenQueryTests
{
    [Fact]
    public async Task Dashboard_usa_solo_la_sede_asignada_y_no_cuenta_mantenimiento_como_disponible()
    {
        await using var context = CreateContext();
        var site = NewSite(1);
        var otherSite = NewSite(2);
        var roomType = new TipoHabitacion { Id = 1, Nombre = "Doble" };
        var free = NewRoom(1, site, roomType, "Disponible");
        var maintenance = NewRoom(2, site, roomType, "Mantenimiento");
        var occupied = NewRoom(3, site, roomType, "Ocupada");
        var confirmedRoom = NewRoom(4, site, roomType, "Disponible");
        var pendingRoom = NewRoom(5, site, roomType, "Disponible");
        var foreignRoom = NewRoom(6, otherSite, roomType, "Disponible");
        context.Habitaciones.AddRange(free, maintenance, occupied, confirmedRoom, pendingRoom, foreignRoom);
        var persona = new Persona { Id = 1, Natural = new PersonaNatural { PersonaId = 1, Nombres = "Cliente" } };
        var client = new Cliente { Id = 1, PersonaId = persona.Id, Persona = persona };
        context.Clientes.Add(client);
        var today = DateTime.UtcNow.Date;
        context.Reservas.AddRange(
            NewReservation(10, client, site, confirmedRoom, "Confirmada", today, 300),
            NewReservation(11, client, site, pendingRoom, "Pendiente", today, 200),
            NewReservation(12, client, otherSite, foreignRoom, "Confirmada", today, 900));
        await context.SaveChangesAsync();
        var handler = new GetDashboardResumenQueryHandler(context, new CurrentUserStub(1));

        var result = await handler.Handle(new GetDashboardResumenQuery(), CancellationToken.None);

        result.HabitacionesOcupadas.Should().Be(2);
        result.HabitacionesDisponibles.Should().Be(1);
        result.ReservasActivas.Should().Be(2);
        result.ReservasPendientes.Should().Be(1);
        result.ReservasConfirmadas.Should().Be(1);
        result.ImporteReservadoMesActual.Should().Be(500);
    }

    [Fact]
    public async Task Dashboard_rechaza_administrador_sin_sede_asignada()
    {
        await using var context = CreateContext();
        var handler = new GetDashboardResumenQueryHandler(context, new CurrentUserStub(null));

        var action = () => handler.Handle(new GetDashboardResumenQuery(), CancellationToken.None);

        await action.Should().ThrowAsync<CasaAndina.Application.Common.Exceptions.ForbiddenAccessException>();
    }

    [Fact]
    public async Task Dashboard_BI_calcula_graficas_y_metricas_desde_reservas_de_la_sede()
    {
        await using var context = CreateContext();
        var site = NewSite(1);
        var otherSite = NewSite(2);
        var roomType = new TipoHabitacion { Id = 1, Nombre = "Doble" };
        var confirmedRoom = NewRoom(1, site, roomType, "Disponible");
        var freeRoom = NewRoom(2, site, roomType, "Disponible");
        var foreignRoom = NewRoom(3, otherSite, roomType, "Disponible");
        context.Habitaciones.AddRange(confirmedRoom, freeRoom, foreignRoom);
        var persona = new Persona { Id = 1, Natural = new PersonaNatural { PersonaId = 1, Nombres = "Cliente" } };
        var client = new Cliente { Id = 1, PersonaId = persona.Id, Persona = persona };
        context.Clientes.Add(client);
        var today = DateTime.UtcNow.Date;
        context.Reservas.AddRange(
            NewReservation(10, client, site, confirmedRoom, "Confirmada", today, 300),
            NewReservation(11, client, site, freeRoom, "Pendiente", today, 200),
            NewReservation(12, client, otherSite, foreignRoom, "Confirmada", today, 900));
        await context.SaveChangesAsync();
        var handler = new GetDashboardBiQueryHandler(context, new CurrentUserStub(1));

        var result = await handler.Handle(new GetDashboardBiQuery("30d"), CancellationToken.None);

        result.SedeNombre.Should().Be("Sede 1");
        result.Kpis.TotalReservas.Should().Be(2);
        result.Kpis.ImporteReservadoSoles.Should().Be(500);
        result.Kpis.TotalClientesActivos.Should().Be(1);
        result.OcupacionSedes.Should().ContainSingle().Which.Cantidad.Should().Be(1);
        result.ReservasSemana.Sum(semana => semana.Confirmadas).Should().Be(1);
        result.ReservasSemana.Sum(semana => semana.Canceladas).Should().Be(0);
        result.Canales.ReservasDirectas.Should().Be(2);
        result.Canales.TicketPromedioSoles.Should().Be(250);
        result.Canales.EstadiaPromedioNoches.Should().Be(1);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private static Sede NewSite(int id) => new()
    {
        Id = id, Nombre = $"Sede {id}", Codigo = $"S{id}", Region = "Sierra", Ciudad = "Cusco", Categoria = "Standard"
    };

    private static Habitacion NewRoom(int id, Sede site, TipoHabitacion type, string status) => new()
    {
        Id = id, SedeId = site.Id, Sede = site, TipoHabitacionId = type.Id, TipoHabitacion = type,
        Numero = id.ToString(), Piso = 1, Estado = status
    };

    private static Reserva NewReservation(int id, Cliente client, Sede site, Habitacion room, string status, DateTime today, decimal total)
    {
        var reservation = new Reserva
        {
            Id = id, CodigoReserva = $"CA-{id}", ClienteId = client.Id, Cliente = client,
            SedeId = site.Id, Sede = site, Estado = status, PrecioTotal = total,
            FechaCreacion = DateTime.UtcNow
        };
        reservation.Habitaciones.Add(new ReservaHabitacion
        {
            Id = id + 100, ReservaId = id, Reserva = reservation, HabitacionId = room.Id, Habitacion = room,
            FechaCheckIn = today, FechaCheckOut = today.AddDays(1), CantidadPersonas = 1,
            PrecioAcordado = total, Subtotal = total
        });
        return reservation;
    }

    private sealed class CurrentUserStub(int? sedeId) : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
        public int? SedeId => sedeId;
        public RolUsuario? Rol => RolUsuario.Administrador;
    }
}
