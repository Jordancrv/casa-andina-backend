using CasaAndina.Application.Sedes.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class SedesPopularesTests
{
    [Fact]
    public void Consulta_puede_traducirse_a_SQL_Server()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=CasaAndinaMetadataOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var query = GetSedesPopularesQueryHandler.ConstruirConsulta(db, new DateTime(2026, 1, 1), new DateTime(2026, 4, 1));
        var sql = query.ToQueryString();
        sql.Should().Contain("COUNT").And.Contain("TOP");
    }
    [Fact]
    public async Task Ordena_sedes_activas_por_reservas_recientes_y_no_incluye_canceladas()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var a = new Sede { Id = 1, Nombre = "Sede A", Codigo = "A", Ciudad = "Lima", Categoria = "Select", Region = "Costa", Activo = true };
        var b = new Sede { Id = 2, Nombre = "Sede B", Codigo = "B", Ciudad = "Cusco", Categoria = "Premium", Region = "Sierra", Activo = true };
        var c = new Sede { Id = 3, Nombre = "Sede inactiva", Codigo = "C", Ciudad = "Lima", Categoria = "Select", Region = "Costa", Activo = false };
        db.Sedes.AddRange(a, b, c);
        foreach (var sede in new[] { a, b, c }) db.Habitaciones.Add(new Habitacion { Sede = sede, Numero = "101", Activo = true });
        db.Reservas.AddRange(
            new Reserva { Id = 1, CodigoReserva = "R1", Sede = a, Estado = "Reservado", FechaCreacion = DateTime.UtcNow.AddDays(-1) },
            new Reserva { Id = 2, CodigoReserva = "R2", Sede = b, Estado = "Reservado", FechaCreacion = DateTime.UtcNow.AddDays(-2) },
            new Reserva { Id = 3, CodigoReserva = "R3", Sede = b, Estado = "Completada", FechaCreacion = DateTime.UtcNow.AddDays(-3) },
            new Reserva { Id = 4, CodigoReserva = "R4", Sede = a, Estado = "Cancelada", FechaCreacion = DateTime.UtcNow.AddDays(-1) },
            new Reserva { Id = 5, CodigoReserva = "R5", Sede = a, Estado = "Reservado", FechaCreacion = DateTime.UtcNow.AddDays(-100) },
            new Reserva { Id = 6, CodigoReserva = "R6", Sede = c, Estado = "Reservado", FechaCreacion = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();
        var result = await new GetSedesPopularesQueryHandler(db).Handle(new(), default);
        result.Select(s => s.Id).Should().Equal(2, 1);
        result.Select(s => s.ReservasRecientes).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Sin_reservas_recientes_no_inventa_un_ranking()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        (await new GetSedesPopularesQueryHandler(db).Handle(new(), default)).Should().BeEmpty();
    }
}
