using CasaAndina.Application.Recepcion;
using CasaAndina.Infrastructure.Persistence;
using CasaAndina.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class AutomatizarEstadiasTests
{
    private static readonly DateTime Entrada = new(2026, 11, 7);
    private static readonly DateTime Salida = new(2026, 11, 10);
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString(), x => x.EnableNullChecks(false))
        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static ReservaHabitacion Seed(ApplicationDbContext db, string estado = "Reservado", string habitacionEstado = "Disponible", bool activo = true)
    {
        var detalle = new ReservaHabitacion {
            Id = 1, FechaCheckIn = Entrada, FechaCheckOut = Salida,
            Reserva = new Reserva { Id = 1, Estado = estado, SedeId = 9, CodigoReserva = "CA-AUTO" },
            Habitacion = new Habitacion { Id = 1, Estado = habitacionEstado, Activo = activo, Numero = "101" }
        };
        db.ReservaHabitaciones.Add(detalle); db.SaveChanges(); return detalle;
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task Checkin_solo_desde_diez_am_Peru(int segundos, bool esperado)
    {
        await using var db = Context(); var detail = Seed(db);
        var now = new DateTime(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc).AddSeconds(segundos);
        var service = new AutomatizarEstadiasService(db);
        (await service.ProcesarAsync(1, false, now, default)).Should().Be(esperado);
        detail.FechaCheckInReal.Should().Be(esperado ? now : null);
        detail.Habitacion.Estado.Should().Be(esperado ? "Ocupada" : "Disponible");
        detail.CheckInPorUsuarioId.Should().BeNull();
        (await service.ProcesarAsync(1, false, now, default)).Should().BeFalse();
        db.Auditoria.Count().Should().Be(esperado ? 1 : 0);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task Checkout_vence_a_medianoche_del_dia_siguiente_no_al_comenzar_dia_salida(int segundos, bool esperado)
    {
        await using var db = Context(); var detail = Seed(db);
        var service = new AutomatizarEstadiasService(db);
        await service.ProcesarAsync(1, false, AutomatizarEstadiasService.EntradaUtc(Entrada), default);
        (await service.ProcesarAsync(1, true, new(2026, 11, 10, 5, 0, 0, DateTimeKind.Utc), default)).Should().BeFalse();
        var limite = new DateTime(2026, 11, 11, 5, 0, 0, DateTimeKind.Utc);
        (await service.ProcesarAsync(1, true, limite.AddSeconds(segundos), default)).Should().Be(esperado);
        detail.FechaCheckOutReal.Should().Be(esperado ? limite : null);
        detail.Habitacion.Estado.Should().Be(esperado ? "Disponible" : "Ocupada");
        detail.Reserva.Estado.Should().Be(esperado ? "Completada" : "Reservado");
        detail.CheckOutPorUsuarioId.Should().BeNull();
        (await service.ProcesarAsync(1, true, limite.AddSeconds(segundos), default)).Should().BeFalse();
        db.Auditoria.Count().Should().Be(esperado ? 2 : 1);
    }

    [Theory]
    [InlineData("Cancelada", "Disponible", true)]
    [InlineData("Completada", "Disponible", true)]
    [InlineData("Pendiente", "Disponible", true)]
    [InlineData("Reservado", "Mantenimiento", true)]
    [InlineData("Reservado", "Bloqueada", true)]
    [InlineData("Reservado", "Disponible", false)]
    public async Task No_ingresa_reservas_invalidas_ni_habitaciones_no_disponibles(string estado, string room, bool activo)
    {
        await using var db = Context(); var detail = Seed(db, estado, room, activo);
        (await new AutomatizarEstadiasService(db).ProcesarAsync(1, false, AutomatizarEstadiasService.EntradaUtc(Entrada), default)).Should().BeFalse();
        detail.FechaCheckInReal.Should().BeNull(); db.Auditoria.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Cancelada")]
    [InlineData("Completada")]
    [InlineData("Pendiente")]
    public async Task No_modifica_reservas_no_vigentes_al_vencer(string estado)
    {
        await using var db = Context(); var detail = Seed(db, estado);
        (await new AutomatizarEstadiasService(db).ProcesarAsync(1, true, AutomatizarEstadiasService.SalidaUtc(Salida), default)).Should().BeFalse();
        detail.FechaCheckOutReal.Should().BeNull(); db.Auditoria.Should().BeEmpty();
    }

    [Fact]
    public async Task Recupera_estadia_vencida_tras_apagado_y_no_repite_auditoria()
    {
        await using var db = Context(); var detail = Seed(db);
        var service = new AutomatizarEstadiasService(db);
        var now = AutomatizarEstadiasService.SalidaUtc(Salida).AddDays(3);
        (await service.ProcesarAsync(1, false, now, default)).Should().BeFalse();
        (await service.ProcesarAsync(1, true, now, default)).Should().BeTrue();
        detail.FechaCheckInReal.Should().Be(AutomatizarEstadiasService.EntradaUtc(Entrada));
        detail.FechaCheckOutReal.Should().Be(AutomatizarEstadiasService.SalidaUtc(Salida));
        detail.Reserva.Estado.Should().Be("Completada");
        (await service.ProcesarAsync(1, true, now, default)).Should().BeFalse();
        var logs = await db.Auditoria.ToListAsync();
        logs.Should().HaveCount(2).And.OnlyContain(a => a.UsuarioId == null && a.SedeId == 9 && a.Despues!.Contains("AUTOMATICO"));
    }

    [Fact]
    public async Task Respeta_checkout_manual_y_bloqueo_para_preparacion()
    {
        await using var db = Context(); var detail = Seed(db, habitacionEstado: "Bloqueada");
        detail.FechaCheckInReal = AutomatizarEstadiasService.EntradaUtc(Entrada);
        detail.FechaCheckOutReal = AutomatizarEstadiasService.SalidaUtc(Salida).AddHours(-8);
        detail.CheckOutPorUsuarioId = 7; await db.SaveChangesAsync();
        var salidaManual = detail.FechaCheckOutReal;
        (await new AutomatizarEstadiasService(db).ProcesarAsync(1, true, AutomatizarEstadiasService.SalidaUtc(Salida), default)).Should().BeFalse();
        detail.FechaCheckOutReal.Should().Be(salidaManual); detail.CheckOutPorUsuarioId.Should().Be(7);
        detail.Habitacion.Estado.Should().Be("Bloqueada"); db.Auditoria.Should().BeEmpty();
    }

    [Fact]
    public async Task No_solapa_ingreso_y_cierra_reserva_solo_cuando_terminan_todos_los_detalles()
    {
        await using var db = Context(); var detail = Seed(db);
        var otra = new ReservaHabitacion { Id = 2, Reserva = detail.Reserva, Habitacion = detail.Habitacion,
            FechaCheckIn = Entrada.AddDays(-2), FechaCheckOut = Entrada.AddDays(-1),
            FechaCheckInReal = AutomatizarEstadiasService.EntradaUtc(Entrada.AddDays(-2)) };
        db.ReservaHabitaciones.Add(otra); await db.SaveChangesAsync();
        var service = new AutomatizarEstadiasService(db);
        var now = AutomatizarEstadiasService.EntradaUtc(Entrada);
        (await service.ProcesarAsync(1, false, now, default)).Should().BeFalse();
        (await service.ProcesarAsync(2, true, now, default)).Should().BeTrue();
        detail.Reserva.Estado.Should().Be("Reservado");
        (await service.ProcesarAsync(1, false, now, default)).Should().BeTrue();
        (await service.ProcesarAsync(1, true, AutomatizarEstadiasService.SalidaUtc(Salida), default)).Should().BeTrue();
        detail.Reserva.Estado.Should().Be("Completada");
    }

    [Theory]
    [InlineData("Mantenimiento")]
    [InlineData("Bloqueada")]
    public async Task Cierre_no_elimina_bloqueos_operativos(string estado)
    {
        await using var db = Context(); var detail = Seed(db, habitacionEstado: estado);
        detail.FechaCheckInReal = AutomatizarEstadiasService.EntradaUtc(Entrada); await db.SaveChangesAsync();
        await new AutomatizarEstadiasService(db).ProcesarAsync(1, true, AutomatizarEstadiasService.SalidaUtc(Salida), default);
        detail.Habitacion.Estado.Should().Be(estado); detail.Reserva.Estado.Should().Be("Completada");
    }

    [Fact]
    public async Task Cierre_atrasado_no_libera_habitacion_de_otra_estadia_abierta()
    {
        await using var db = Context(); var detail = Seed(db, habitacionEstado: "Ocupada");
        db.ReservaHabitaciones.Add(new ReservaHabitacion { Id = 2, Habitacion = detail.Habitacion,
            Reserva = new Reserva { Id = 2, Estado = "Reservado", SedeId = 9, CodigoReserva = "CA-OTRA" },
            FechaCheckIn = Salida, FechaCheckOut = Salida.AddDays(3), FechaCheckInReal = AutomatizarEstadiasService.EntradaUtc(Salida) });
        await db.SaveChangesAsync();
        await new AutomatizarEstadiasService(db).ProcesarAsync(1, true, AutomatizarEstadiasService.SalidaUtc(Salida), default);
        detail.Habitacion.Estado.Should().Be("Ocupada");
        db.ChangeTracker.Clear();
        (await new AutomatizarEstadiasService(db).ProcesarAsync(1, true, AutomatizarEstadiasService.SalidaUtc(Salida), default)).Should().BeFalse();
        db.Auditoria.Count().Should().Be(2);
    }

    [Fact]
    public void Horarios_cruzan_fin_de_mes_y_de_anio_en_Peru()
    {
        AutomatizarEstadiasService.SalidaUtc(new(2026, 12, 31)).Should().Be(new DateTime(2027, 1, 1, 5, 0, 0, DateTimeKind.Utc));
        AutomatizarEstadiasService.SalidaUtc(new(2028, 2, 29)).Should().Be(new DateTime(2028, 3, 1, 5, 0, 0, DateTimeKind.Utc));
    }
}
