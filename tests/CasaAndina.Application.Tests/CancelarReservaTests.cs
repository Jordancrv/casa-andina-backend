using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.Commands;
using CasaAndina.Application.Reservas.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class CancelarReservaTests
{
    private sealed record Actor(int? ClienteId, int? UsuarioId = 1) : ICurrentUserService { public int? SedeId => null; public RolUsuario? Rol => RolUsuario.Cliente; }
    private static async Task<ApplicationDbContext> Seed(string estado = "Reservado", DateTime? fecha = null, DateTime? checkInReal = null)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var sede = new Sede { Id = 1, Nombre = "Sede", Codigo = "S1", Ciudad = "Lima", Region = "Costa", Categoria = "Select", Activo = true };
        var room = new Habitacion { Id = 1, Sede = sede, Numero = "101", Activo = true, Estado = "Disponible", PrecioNoche = 100, TipoHabitacion = new TipoHabitacion { Id = 1, Nombre = "Doble", CapacidadAdultos = 2 } };
        var entrada = fecha ?? PeruBusinessTime.Today.AddDays(3);
        db.Reservas.Add(new Reserva { Id = 1, CodigoReserva = "CA-TEST1", ClienteId = 1, Sede = sede, Estado = estado, Habitaciones = [new ReservaHabitacion { Habitacion = room, FechaCheckIn = entrada, FechaCheckOut = entrada.AddDays(2), FechaCheckInReal = checkInReal }] });
        await db.SaveChangesAsync(); return db;
    }

    [Fact]
    public async Task Cancelacion_propia_libera_disponibilidad_y_se_registra_una_sola_vez()
    {
        await using var db = await Seed(); var handler = new CancelarReservaCommandHandler(db, new Actor(1));
        var entrada = PeruBusinessTime.Today.AddDays(3);
        (await new GetDisponibilidadQueryHandler(db).Handle(new(1, entrada, entrada.AddDays(2)), default)).Should().BeEmpty();
        await handler.Handle(new(1), default);
        (await db.Reservas.SingleAsync()).Estado.Should().Be("Cancelada");
        (await new GetDisponibilidadQueryHandler(db).Handle(new(1, entrada, entrada.AddDays(2)), default)).Should().ContainSingle();
        await handler.Handle(new(1), default);
        (await db.Auditoria.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("Completada", false, false)]
    [InlineData("Reservado", true, false)]
    [InlineData("Reservado", false, true)]
    public async Task No_cancela_una_estadia_finalizada_iniciada_o_con_fecha_pasada(string estado, bool checkIn, bool pasada)
    {
        await using var db = await Seed(estado, pasada ? PeruBusinessTime.Today.AddDays(-1) : null, checkIn ? DateTime.UtcNow : null);
        var action = () => new CancelarReservaCommandHandler(db, new Actor(1)).Handle(new(1), default);
        await action.Should().ThrowAsync<ConflictException>();
        (await db.Reservas.SingleAsync()).Estado.Should().Be(estado);
    }

    [Fact]
    public async Task Disponibilidad_devuelve_las_imagenes_en_el_orden_del_album()
    {
        await using var db = await Seed();
        var room = await db.Habitaciones.SingleAsync();
        room.FotoUrl = "https://example.test/principal.jpg";
        room.Imagenes.Add(new HabitacionImagen { Orden = 2, Url = "https://example.test/segunda.jpg" });
        room.Imagenes.Add(new HabitacionImagen { Orden = 1, Url = room.FotoUrl });
        await db.SaveChangesAsync();
        var entrada = PeruBusinessTime.Today.AddDays(10);
        var disponible = (await new GetDisponibilidadQueryHandler(db).Handle(new(1, entrada, entrada.AddDays(2)), default)).Should().ContainSingle().Subject;
        disponible.FotoUrl.Should().Be("https://example.test/principal.jpg");
        var imagenes = await new GetHabitacionImagenesQueryHandler(db).Handle(new(room.Id), default);
        imagenes.Should().Equal("https://example.test/principal.jpg", "https://example.test/segunda.jpg");
    }

    [Fact]
    public async Task No_cancela_reserva_de_otro_cliente_ni_sin_identidad()
    {
        await using var db = await Seed();
        var foreign = () => new CancelarReservaCommandHandler(db, new Actor(2)).Handle(new(1), default);
        await foreign.Should().ThrowAsync<NotFoundException>();
        var anonymous = () => new CancelarReservaCommandHandler(db, new Actor(null)).Handle(new(1), default);
        await anonymous.Should().ThrowAsync<UnauthorizedAccessException>();
        (await db.Reservas.SingleAsync()).Estado.Should().Be("Reservado");
    }
}
