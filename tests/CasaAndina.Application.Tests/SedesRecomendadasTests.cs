using CasaAndina.Application.Common;
using CasaAndina.Application.Sedes.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class SedesRecomendadasTests
{
    [Fact]
    public async Task Filtra_capacidad_precio_y_disponibilidad_priorizando_reservas_recientes()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var a = new Sede { Id = 1, Nombre = "A", Codigo = "A", Ciudad = "Lima", Categoria = "Select", Region = "Costa", Activo = true };
        var b = new Sede { Id = 2, Nombre = "B", Codigo = "B", Ciudad = "Cusco", Categoria = "Select", Region = "Sierra", Activo = true };
        var tipo = new TipoHabitacion { Id = 1, Nombre = "Doble", CapacidadAdultos = 2 };
        db.Habitaciones.AddRange(new Habitacion { Id = 1, Sede = a, Numero = "101", TipoHabitacion = tipo, Activo = true, Estado = "Disponible", PrecioNoche = 100 }, new Habitacion { Id = 2, Sede = b, Numero = "101", TipoHabitacion = tipo, Activo = true, Estado = "Disponible", PrecioNoche = 200 });
        var entrada = PeruBusinessTime.Today.AddDays(4);
        db.Reservas.AddRange(new Reserva { Id = 1, CodigoReserva = "R1", Sede = b, Estado = "Completada", FechaCreacion = DateTime.UtcNow.AddDays(-1) },
            new Reserva { Id = 2, CodigoReserva = "R2", Sede = a, Estado = "Reservado", FechaCreacion = DateTime.UtcNow.AddDays(-1), Habitaciones = [new ReservaHabitacion { HabitacionId = 1, FechaCheckIn = entrada, FechaCheckOut = entrada.AddDays(2) }] });
        await db.SaveChangesAsync();
        var handler = new GetSedesRecomendadasQueryHandler(db);
        var available = await handler.Handle(new(entrada, 2, 2, 300), default);
        available.Should().ContainSingle().Which.HabitacionId.Should().Be(2);
        (await handler.Handle(new(entrada, 2, 3, 300), default)).Should().BeEmpty();
        (await handler.Handle(new(entrada, 2, 2, 150), default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Rechaza_datos_fuera_de_rango()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var handler = new GetSedesRecomendadasQueryHandler(db);
        foreach (var input in new[] { new GetSedesRecomendadasQuery(PeruBusinessTime.Today.AddDays(-1), 1, 1, 200), new(PeruBusinessTime.Today, 31, 1, 200), new(PeruBusinessTime.Today, 1, 9, 200), new(PeruBusinessTime.Today, 1, 1, 0) })
        {
            var action = () => handler.Handle(input, default);
            await action.Should().ThrowAsync<CasaAndina.Application.Common.Exceptions.ValidationException>();
        }
    }
}
