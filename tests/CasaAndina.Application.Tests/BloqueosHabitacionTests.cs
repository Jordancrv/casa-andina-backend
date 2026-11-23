using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Habitaciones.Commands;
using CasaAndina.Application.Reservas.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class BloqueosHabitacionTests
{
    private sealed record Actor(int? SedeId = 1, bool Permitido = true) : ICurrentUserService
    { public int? UsuarioId => 1; public int? ClienteId => null; public RolUsuario? Rol => RolUsuario.Administrador; public bool TienePermiso(string p) => Permitido && p == "habitaciones.editar"; }
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static async Task Seed(ApplicationDbContext db)
    {
        var sede = new Sede { Id = 1, Nombre = "Sede", Codigo = "S", Ciudad = "Lima", Region = "Costa", Categoria = "Select", Activo = true };
        db.Habitaciones.Add(new Habitacion { Id = 1, Sede = sede, Numero = "101", Activo = true, Estado = "Disponible", PrecioNoche = 100, TipoHabitacion = new TipoHabitacion { Id = 1, Nombre = "Doble", CapacidadAdultos = 2 } });
        await db.SaveChangesAsync();
    }
    [Fact]
    public async Task Bloqueo_incluye_fin_excluye_disponibilidad_y_cancelacion_la_recupera()
    {
        await using var db = Context(); await Seed(db); var inicio = PeruBusinessTime.Today.AddDays(2);
        var id = await new CrearBloqueoHandler(db, new Actor()).Handle(new(1, inicio, inicio.AddDays(1), "Mantenimiento"), default);
        var bloqueo = await db.BloqueosHabitacion.SingleAsync(); bloqueo.FinExclusivo.Should().Be(inicio.AddDays(2));
        var disponibilidad = new GetDisponibilidadQueryHandler(db);
        (await disponibilidad.Handle(new(1, inicio.AddDays(1), inicio.AddDays(2)), default)).Should().BeEmpty();
        (await disponibilidad.Handle(new(1, inicio.AddDays(2), inicio.AddDays(3)), default)).Should().ContainSingle();
        await new CancelarBloqueoHandler(db, new Actor()).Handle(new(id), default);
        (await disponibilidad.Handle(new(1, inicio, inicio.AddDays(1)), default)).Should().ContainSingle();
        (await db.Auditoria.CountAsync()).Should().Be(2);
    }
    [Fact]
    public async Task Rechaza_solapamientos_y_otras_sedes()
    {
        await using var db = Context(); await Seed(db); var inicio = PeruBusinessTime.Today.AddDays(5);
        var handler = new CrearBloqueoHandler(db, new Actor()); await handler.Handle(new(1, inicio, inicio, "Limpieza"), default);
        await FluentActions.Invoking(() => handler.Handle(new(1, inicio, inicio.AddDays(1), "Otro"), default)).Should().ThrowAsync<ConflictException>();
        await FluentActions.Invoking(() => new CrearBloqueoHandler(db, new Actor(SedeId: 2)).Handle(new(1, inicio, inicio, "Otro"), default)).Should().ThrowAsync<NotFoundException>();
        await FluentActions.Invoking(() => new CrearBloqueoHandler(db, new Actor(Permitido: false)).Handle(new(1, inicio, inicio, "Otro"), default)).Should().ThrowAsync<ForbiddenAccessException>();
    }
    [Fact]
    public async Task Rechaza_bloqueo_si_hay_reserva_en_las_fechas()
    {
        await using var db = Context(); await Seed(db); var fecha = PeruBusinessTime.Today.AddDays(4);
        db.Reservas.Add(new Reserva { CodigoReserva = "CA-TEST", ClienteId = 1, SedeId = 1, Estado = "Reservado", Habitaciones = [new ReservaHabitacion { HabitacionId = 1, FechaCheckIn = fecha, FechaCheckOut = fecha.AddDays(2) }] }); await db.SaveChangesAsync();
        await FluentActions.Invoking(() => new CrearBloqueoHandler(db, new Actor()).Handle(new(1, fecha.AddDays(1), fecha.AddDays(1), "Mantenimiento"), default)).Should().ThrowAsync<ConflictException>();
    }
    [Fact]
    public void Rechaza_fechas_invalidas()
    {
        var validator = new CrearBloqueoValidator(); var today = PeruBusinessTime.Today;
        validator.Validate(new CrearBloqueoCommand(1, today, today.AddDays(-1), "Mantenimiento")).IsValid.Should().BeFalse();
        validator.Validate(new CrearBloqueoCommand(1, today, today, "Mantenimiento")).IsValid.Should().BeTrue();
    }
}
