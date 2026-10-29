using CasaAndina.Application.Clientes.Commands;
using CasaAndina.Application.Clientes.Queries;
using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ClientesCrmSqlServerTests
{
    [SqlServerFact]
    public async Task Crm_persiste_altas_edicion_segmentacion_y_filtros_en_base_aislada()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CASAANDINA_TEST_SQLSERVER"))
        { InitialCatalog = $"CasaAndinaTestCrm_{Guid.NewGuid():N}", MultipleActiveResultSets = true };
        connection.InitialCatalog.Should().StartWith("CasaAndinaTestCrm_");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection.ConnectionString).Options);
        var created = false;
        try
        {
            created = await db.Database.EnsureCreatedAsync(); created.Should().BeTrue();
            var sede = new Sede { Nombre = "Sede CRM", Codigo = "CRM", Region = "Costa", Ciudad = "Lima", Categoria = "Standard" };
            var otra = new Sede { Nombre = "Otra CRM", Codigo = "OTR", Region = "Sierra", Ciudad = "Cusco", Categoria = "Standard" };
            var doc = new TipoDocumento { Codigo = "DNI", Nombre = "DNI", TipoPersonaPermitida = "N", LongitudMinima = 8, LongitudMaxima = 8 };
            db.AddRange(sede, otra, doc); await db.SaveChangesAsync();
            var user = new User(sede.Id);
            var datos = new ClienteCrmDatos("N", "Ana", "Torres", null, doc.Id, "12345678", "crm@example.test", "+51987654321");
            var handler = new GuardarClienteCommandHandler(db, user);
            var id = await handler.Handle(new CrearClienteCommand(datos), default);
            db.ChangeTracker.Clear();
            var query = new GetClientesAdminQueryHandler(db, user);
            var inicial = await query.Handle(new(Buscar: "12345678"), default);
            inicial.Items.Should().ContainSingle().Which.SedeRegistroId.Should().Be(sede.Id);
            inicial.Kpis.NuevosEsteMes.Should().Be(1);
            var cliente = await db.Clientes.SingleAsync(c => c.Id == id);
            var tipo = new TipoHabitacion { Nombre = "Doble", CapacidadAdultos = 2 };
            var habitacion = new Habitacion { SedeId = sede.Id, TipoHabitacion = tipo, Numero = "101", Piso = 1, PrecioNoche = 200, Estado = "Disponible" };
            db.Habitaciones.Add(habitacion);
            for (var i = 0; i < 3; i++) db.Reservas.Add(new Reserva
            {
                ClienteId = id, SedeId = sede.Id, CodigoReserva = $"CA-CRM-{i}", Estado = i < 2 ? "Completada" : "Reservado", PrecioTotal = 200,
                Habitaciones = { new ReservaHabitacion { Habitacion = habitacion, FechaCheckIn = PeruBusinessTime.Today.AddDays(i * 2),
                    FechaCheckOut = PeruBusinessTime.Today.AddDays(i * 2 + 1), CantidadPersonas = 1, PrecioAcordado = 200, Subtotal = 200 } }
            });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var frecuente = await query.Handle(new(Buscar: "Ana Torres", Categoria: "Frecuente", Actividad: "ConReservasActivas"), default);
            frecuente.Items.Should().ContainSingle().Which.EstadiasCompletadas.Should().Be(2);
            frecuente.Kpis.ConReservasActivas.Should().Be(1);
            frecuente.Items.Single().Historial.Should().HaveCount(3);
            await handler.Handle(new ActualizarClienteCommand(id, datos with { Estado = "Inactivo", Telefono = "+51999999999", SedeRegistroId = sede.Id }), default);
            db.ChangeTracker.Clear();
            var actualizado = await query.Handle(new(Estado: "Inactivo", SedeId: sede.Id), default);
            actualizado.Items.Should().ContainSingle().Which.Telefono.Should().Be("+51999999999");
            (await new GetClientesAdminQueryHandler(db, new User(otra.Id)).Handle(new(), default)).Items.Should().BeEmpty();
        }
        finally { if (created) await db.Database.EnsureDeletedAsync(); }
    }

    [SqlServerFact]
    public async Task Migracion_conserva_clientes_existentes_y_puede_ejecutarse_dos_veces()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CASAANDINA_TEST_SQLSERVER"))
        { InitialCatalog = $"CasaAndinaTestCrm_{Guid.NewGuid():N}" };
        connection.InitialCatalog.Should().StartWith("CasaAndinaTestCrm_");
        await using var master = new SqlConnection(new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" }.ConnectionString);
        await master.OpenAsync();
        var created = false;
        try
        {
            await new SqlCommand($"CREATE DATABASE [{connection.InitialCatalog}]", master).ExecuteNonQueryAsync(); created = true;
            await using var target = new SqlConnection(connection.ConnectionString); await target.OpenAsync();
            await new SqlCommand("CREATE TABLE dbo.Sede(SedeId int PRIMARY KEY); CREATE TABLE dbo.Cliente(ClienteId int PRIMARY KEY); INSERT dbo.Cliente VALUES(1);", target).ExecuteNonQueryAsync();
            var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../scripts/migrate-crm-clientes.sql"));
            var script = await File.ReadAllTextAsync(scriptPath);
            await new SqlCommand(script, target).ExecuteNonQueryAsync();
            await new SqlCommand(script, target).ExecuteNonQueryAsync();
            (await new SqlCommand("SELECT COUNT(*) FROM dbo.Cliente WHERE ClienteId=1 AND SedeRegistroId IS NULL", target).ExecuteScalarAsync()).Should().Be(1);
        }
        finally
        {
            if (created)
            {
                SqlConnection.ClearAllPools();
                await new SqlCommand($"ALTER DATABASE [{connection.InitialCatalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{connection.InitialCatalog}];", master).ExecuteNonQueryAsync();
            }
        }
    }

    private sealed class User(int sede) : ICurrentUserService
    { public int? UsuarioId => null; public int? ClienteId => null; public int? SedeId => sede; public RolUsuario? Rol => RolUsuario.Administrador; }
    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAANDINA_TEST_SQLSERVER"))) Skip = "Defina CASAANDINA_TEST_SQLSERVER para las pruebas SQL aisladas."; }
    }
}
