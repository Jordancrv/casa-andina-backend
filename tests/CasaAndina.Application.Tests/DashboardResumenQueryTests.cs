using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common;
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
    public async Task Dashboard_ordena_habitaciones_numericamente_y_servicios_por_unidades_del_periodo_y_sede()
    {
        await using var context = CreateContext();
        var site = NewSite(1); var other = NewSite(2); var type = new TipoHabitacion { Id = 1, Nombre = "Doble" };
        var first = NewRoom(1, site, type, "Disponible"); first.Numero = "2";
        var second = NewRoom(2, site, type, "Disponible"); second.Numero = "10";
        var third = NewRoom(3, site, type, "Disponible"); third.Numero = "101";
        var foreign = NewRoom(4, other, type, "Disponible");
        context.Habitaciones.AddRange(first, second, third, foreign);
        var client = new Cliente { Id = 1, Persona = new Persona { Id = 1, Natural = new PersonaNatural { Nombres = "Cliente" } } };
        context.Clientes.Add(client);
        var desayuno = new Servicio { Id = 1, Nombre = "Desayuno" }; var traslado = new Servicio { Id = 2, Nombre = "Traslado" };
        var today = PeruBusinessTime.Today;
        var one = NewReservation(10, client, site, first, "Reservado", today, 100);
        var two = NewReservation(11, client, site, second, "Completada", today, 100);
        var cancelled = NewReservation(12, client, site, third, "Cancelada", today, 100);
        var otherSite = NewReservation(13, client, other, foreign, "Reservado", today, 100);
        var old = NewReservation(14, client, site, third, "Reservado", today, 100); old.FechaCreacion = DateTime.UtcNow.AddDays(-40);
        one.Servicios.Add(new ReservaServicio { Servicio = desayuno, Cantidad = 2 });
        one.Servicios.Add(new ReservaServicio { Servicio = traslado, Cantidad = 1 });
        two.Servicios.Add(new ReservaServicio { Servicio = desayuno, Cantidad = 1 });
        cancelled.Servicios.Add(new ReservaServicio { Servicio = traslado, Cantidad = 100 });
        otherSite.Servicios.Add(new ReservaServicio { Servicio = traslado, Cantidad = 100 });
        old.Servicios.Add(new ReservaServicio { Servicio = traslado, Cantidad = 100 });
        context.Reservas.AddRange(one, two, cancelled, otherSite, old); await context.SaveChangesAsync();
        var result = await new GetDashboardBiQueryHandler(context, new CurrentUserStub(1)).Handle(new GetDashboardBiQuery("7d"), default);
        result.OcupacionHabitaciones!.Select(h => h.Numero).Should().Equal("2", "10", "101");
        result.ServiciosPopulares!.Select(s => s.Nombre).Should().Equal("Desayuno", "Traslado");
        result.ServiciosPopulares![0].Cantidad.Should().Be(3); result.ServiciosPopulares[0].Reservas.Should().Be(2);
        result.ServiciosPopulares[1].Cantidad.Should().Be(1);
    }
    [Fact]
    public async Task Ocupacion_por_habitacion_usa_noches_del_periodo_y_no_duplica_solapamientos()
    {
        await using var context = CreateContext();
        var site = NewSite(1); var other = NewSite(2); var type = new TipoHabitacion { Id = 1, Nombre = "Doble" };
        var room = NewRoom(1, site, type, "Disponible"); var empty = NewRoom(2, site, type, "Disponible");
        var foreign = NewRoom(3, other, type, "Disponible");
        context.Habitaciones.AddRange(room, empty, foreign);
        var client = new Cliente { Id = 1, Persona = new Persona { Id = 1, Natural = new PersonaNatural { Nombres = "Cliente" } } };
        context.Clientes.Add(client);
        var today = PeruBusinessTime.Today;
        var first = NewReservation(10, client, site, room, "Completada", today.AddDays(-10), 100);
        first.Habitaciones.Single().FechaCheckOut = today.AddDays(-3);
        var overlap = NewReservation(11, client, site, room, "Reservado", today.AddDays(-5), 100);
        overlap.Habitaciones.Single().FechaCheckOut = today.AddDays(-2);
        var cancelled = NewReservation(12, client, site, empty, "Cancelada", today.AddDays(-2), 100);
        var outside = NewReservation(13, client, site, empty, "Reservado", today.AddDays(1), 100);
        var otherSite = NewReservation(14, client, other, foreign, "Reservado", today.AddDays(-2), 100);
        context.Reservas.AddRange(first, overlap, cancelled, outside, otherSite); await context.SaveChangesAsync();
        var handler = new GetDashboardBiQueryHandler(context, new CurrentUserStub(1));
        var week = await handler.Handle(new GetDashboardBiQuery("7d"), default);
        week.OcupacionHabitaciones.Should().HaveCount(2);
        week.OcupacionHabitaciones!.Select(h => h.HabitacionId).Should().Equal(room.Id, empty.Id);
        var booked = week.OcupacionHabitaciones!.Single(h => h.HabitacionId == room.Id);
        booked.NochesReservadas.Should().Be(4); booked.NochesPeriodo.Should().Be(7); booked.OcupacionPct.Should().Be(57.1m);
        week.OcupacionHabitaciones!.Single(h => h.HabitacionId == empty.Id).NochesReservadas.Should().Be(0);
        var month = await handler.Handle(new GetDashboardBiQuery("30d"), default);
        month.OcupacionHabitaciones!.Single(h => h.HabitacionId == room.Id).NochesReservadas.Should().Be(8);
    }
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
        var today = PeruBusinessTime.Today;
        context.Reservas.AddRange(
            NewReservation(10, client, site, confirmedRoom, "Reservado", today, 300),
            NewReservation(11, client, site, pendingRoom, "Pendiente", today, 200),
            NewReservation(12, client, otherSite, foreignRoom, "Reservado", today, 900));
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
        var today = PeruBusinessTime.Today;
        context.Reservas.AddRange(
            NewReservation(10, client, site, confirmedRoom, "Reservado", today, 300),
            NewReservation(11, client, site, freeRoom, "Pendiente", today, 200),
            NewReservation(12, client, otherSite, foreignRoom, "Reservado", today, 900));
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

    [Fact]
    public async Task Dashboard_BI_rechaza_usuario_sin_sede()
    {
        await using var context = CreateContext();
        var handler = new GetDashboardBiQueryHandler(context, new CurrentUserStub(null));

        var action = () => handler.Handle(new GetDashboardBiQuery("30d"), CancellationToken.None);

        await action.Should().ThrowAsync<CasaAndina.Application.Common.Exceptions.ForbiddenAccessException>();
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
