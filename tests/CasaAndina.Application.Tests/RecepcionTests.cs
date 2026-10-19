using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Recepcion;
using CasaAndina.Application.Reservas.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class RecepcionTests
{
    private sealed record User(RolUsuario? Rol = RolUsuario.Recepcion, int? SedeId = 1, int? UsuarioId = 7, int? ClienteId = null) : ICurrentUserService;
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString(), x => x.EnableNullChecks(false))
        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static Reserva Seed(ApplicationDbContext context, int sede = 1)
    {
        var reservation = new Reserva { Id = 1, CodigoReserva = "CA-TEST00001", SedeId = sede, ClienteId = 10,
            Estado = "Confirmada", Sede = new Sede { Id = sede, Nombre = "Prueba", Ciudad = "Cusco" } };
        for (var id = 1; id <= 2; id++)
            reservation.Habitaciones.Add(new ReservaHabitacion { Id = id, Reserva = reservation, HabitacionId = id,
                FechaCheckIn = PeruBusinessTime.Today, FechaCheckOut = PeruBusinessTime.Today.AddDays(2),
                Habitacion = new Habitacion { Id = id, Numero = id.ToString(), SedeId = sede, Activo = true,
                    Estado = "Disponible", TipoHabitacion = new TipoHabitacion { Nombre = "Doble" } } });
        context.Reservas.Add(reservation); context.SaveChanges(); return reservation;
    }

    [Fact]
    public async Task Checkin_y_checkout_por_habitacion_completan_reserva_solo_al_final()
    {
        await using var context = Context();
        var reservation = Seed(context);
        var handler = new RegistrarMovimientoCommandHandler(context, new User());
        await handler.Handle(new(1, false), default);
        reservation.Habitaciones.First().Habitacion.Estado.Should().Be("Ocupada");
        reservation.Habitaciones.First().CheckInPorUsuarioId.Should().Be(7);
        var client = new GetReservasQueryHandler(context, new User(RolUsuario.Cliente, null, 10, 10));
        (await client.Handle(new(), default)).Single().EstadoEstadia.Should().Be("Estadía en curso");
        await handler.Handle(new(1, true), default);
        reservation.Estado.Should().Be("Confirmada");
        reservation.Habitaciones.First().Habitacion.Estado.Should().Be("Bloqueada");
        await handler.Handle(new(2, false), default);
        await handler.Handle(new(2, true), default);
        reservation.Estado.Should().Be("Completada");
        context.Auditoria.Count().Should().Be(4);
        (await client.Handle(new(), default)).Single().EstadoEstadia.Should().Be("Estadía finalizada");
    }

    [Theory]
    [InlineData(RolUsuario.Cliente)]
    [InlineData(RolUsuario.Operaciones)]
    [InlineData(RolUsuario.Mantenimiento)]
    public void Otros_roles_no_pueden_operar_recepcion(RolUsuario role)
        => FluentActions.Invoking(() => RecepcionRules.Sede(new User(role))).Should().Throw<ForbiddenAccessException>();

    [Fact]
    public void Cuenta_sin_sede_no_puede_operar()
        => FluentActions.Invoking(() => RecepcionRules.Sede(new User(SedeId: null))).Should().Throw<ForbiddenAccessException>();

    [Fact]
    public async Task No_se_puede_operar_una_reserva_de_otra_sede()
    {
        await using var context = Context(); Seed(context, 2);
        await FluentActions.Awaiting(() => new RegistrarMovimientoCommandHandler(context, new User()).Handle(new(1, false), default))
            .Should().ThrowAsync<NotFoundException>();
        (await new GetRecepcionQueryHandler(context, new User()).Handle(new(Codigo: "CA-TEST00001"), default)).Should().BeEmpty();
    }

    [Fact]
    public async Task No_se_permiten_checkin_duplicado_ni_checkout_sin_ingreso()
    {
        await using var context = Context(); Seed(context);
        var handler = new RegistrarMovimientoCommandHandler(context, new User());
        await FluentActions.Awaiting(() => handler.Handle(new(1, true), default)).Should().ThrowAsync<ConflictException>();
        await handler.Handle(new(1, false), default);
        await FluentActions.Awaiting(() => handler.Handle(new(1, false), default)).Should().ThrowAsync<ConflictException>();
        await handler.Handle(new(1, true), default);
        await FluentActions.Awaiting(() => handler.Handle(new(1, true), default)).Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Otra_estadia_abierta_impide_ingresar_a_la_misma_habitacion()
    {
        await using var context = Context();
        var reservation = Seed(context);
        var detail = reservation.Habitaciones.First();
        context.ReservaHabitaciones.Add(new ReservaHabitacion
        {
            Id = 99, ReservaId = reservation.Id, HabitacionId = detail.HabitacionId,
            FechaCheckIn = PeruBusinessTime.Today.AddDays(-1), FechaCheckOut = PeruBusinessTime.Today,
            FechaCheckInReal = DateTime.UtcNow.AddHours(-5), CheckInPorUsuarioId = 7
        });
        await context.SaveChangesAsync();
        await FluentActions.Awaiting(() => new RegistrarMovimientoCommandHandler(context, new User())
            .Handle(new(1, false), default)).Should().ThrowAsync<ConflictException>();
        detail.FechaCheckInReal.Should().BeNull();
    }

    [Theory]
    [InlineData("Cancelada", "Disponible", 0)]
    [InlineData("Completada", "Disponible", 0)]
    [InlineData("Confirmada", "Mantenimiento", 0)]
    [InlineData("Confirmada", "Bloqueada", 0)]
    [InlineData("Confirmada", "Disponible", 2)]
    [InlineData("Confirmada", "Disponible", -5)]
    public void Se_rechaza_ingreso_si_no_corresponde_estado_o_fecha(string estado, string roomState, int offset)
    {
        var detail = new ReservaHabitacion { Reserva = new Reserva { Estado = estado },
            Habitacion = new Habitacion { Activo = true, Estado = roomState },
            FechaCheckIn = PeruBusinessTime.Today.AddDays(offset), FechaCheckOut = PeruBusinessTime.Today.AddDays(offset + 2) };
        FluentActions.Invoking(() => RecepcionRules.ValidarMovimiento(detail, false, PeruBusinessTime.Today)).Should().Throw<ConflictException>();
    }

    [Fact]
    public async Task Busqueda_y_pestanas_separan_llegadas_y_estadias()
    {
        await using var context = Context(); Seed(context);
        var query = new GetRecepcionQueryHandler(context, new User());
        (await query.Handle(new(), default)).Should().HaveCount(2);
        await new RegistrarMovimientoCommandHandler(context, new User()).Handle(new(1, false), default);
        (await query.Handle(new(), default)).Should().HaveCount(1);
        (await query.Handle(new("estadia"), default)).Should().HaveCount(1);
        (await query.Handle(new("salidas"), default)).Should().BeEmpty();
        (await query.Handle(new(Codigo: "CA-TEST00001"), default)).Should().HaveCount(2);
    }
}
