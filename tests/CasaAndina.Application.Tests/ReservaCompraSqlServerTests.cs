using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common;
using CasaAndina.Application.Reservas.Commands;
using CasaAndina.Application.Reservas.DTOs;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ReservaCompraSqlServerTests
{
    [SqlServerFact]
    public async Task Compra_persiste_reserva_huesped_comprobante_y_pago_en_base_aislada()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("CASAANDINA_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
            throw new InvalidOperationException("La prueba SQL Server requiere CASAANDINA_TEST_SQLSERVER.");

        var databaseName = $"CasaAndinaTestCompra_{Guid.NewGuid():N}";
        var masterConnection = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master",
            MultipleActiveResultSets = true
        };
        masterConnection.Remove("AttachDbFilename");
        var targetConnection = new SqlConnectionStringBuilder(masterConnection.ConnectionString)
        {
            InitialCatalog = databaseName
        };
        databaseName.Should().MatchRegex("^CasaAndinaTestCompra_[0-9a-f]{32}$");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(targetConnection.ConnectionString)
            .Options;
        var databaseCreated = false;
        var cleanupAttempted = false;

        try
        {
            await using (var connection = new SqlConnection(masterConnection.ConnectionString))
            {
                await connection.OpenAsync();
                await using var create = connection.CreateCommand();
                create.CommandText = $"CREATE DATABASE [{databaseName}]";
                await create.ExecuteNonQueryAsync();
                databaseCreated = true;
            }

            await using (var seedContext = new ApplicationDbContext(options))
            {
                (await seedContext.Database.EnsureCreatedAsync()).Should().BeTrue("cada ejecución utiliza una base con nombre aleatorio");
                SeedCheckout(seedContext);
            }

            ReservaCompraResponseDto result;
            await using (var writeContext = new ApplicationDbContext(options))
            {
                var handler = new CrearReservaCompraCommandHandler(writeContext, new ClientUserStub(1));
                result = await handler.Handle(new CrearReservaCompraCommand(
                    1,
                    PeruBusinessTime.Today.AddDays(2),
                    PeruBusinessTime.Today.AddDays(4),
                    [new HabitacionCompraDto(1, [
                        new HuespedCompraDto("Cliente", "Integración", "cliente@example.test", null, true),
                        new HuespedCompraDto("Huésped", "Prueba", "guest@example.test", null, false)
                    ])],
                    [],
                    "Boleta",
                    null,
                    null,
                    "YAPE"),
                    CancellationToken.None);
            }

            await using (var verifyContext = new ApplicationDbContext(options))
            {
                var reservation = await verifyContext.Reservas
                    .Include(item => item.Habitaciones).ThenInclude(item => item.Huespedes)
                        .ThenInclude(item => item.Huesped).ThenInclude(item => item.PersonaNatural)
                    .Include(item => item.Comprobante)
                    .SingleAsync(item => item.Id == result.ReservaId);
                var payment = await verifyContext.Pagos
                    .Include(item => item.MetodoPago)
                    .Include(item => item.EstadoPago)
                    .Include(item => item.Detalles)
                    .SingleAsync(item => item.Detalles.Any(detail => detail.ReservaId == result.ReservaId));

                reservation.Estado.Should().Be("Confirmada");
                reservation.PrecioTotal.Should().Be(200m);
                reservation.Habitaciones.Should().ContainSingle();
                reservation.Habitaciones.Single().CantidadPersonas.Should().Be(1);
                reservation.Habitaciones.Single().Huespedes.Should().HaveCount(2);
                reservation.Habitaciones.Single().Huespedes
                    .Should().Contain(item => item.Huesped.PersonaNatural.Nombres == "Cliente");
                reservation.Habitaciones.Single().Huespedes
                    .Should().Contain(item => item.Huesped.PersonaNatural.Nombres == "Huésped");
                var clientPersonId = await verifyContext.Clientes.Select(item => item.PersonaId).SingleAsync();
                reservation.Habitaciones.Single().Huespedes
                    .Should().Contain(item => item.Huesped.PersonaId == clientPersonId, "the account email reuses the client's person");
                reservation.Comprobante.Should().NotBeNull();
                reservation.Comprobante!.Tipo.Should().Be(TipoComprobante.Boleta);
                payment.MetodoPago.Codigo.Should().Be("YAPE");
                payment.EstadoPago.Codigo.Should().Be("APROBADO");
                payment.Importe.Should().Be(200m);
                payment.ReferenciaProveedor.Should().BeNull();
                payment.Detalles.Should().ContainSingle().Which.MontoAplicado.Should().Be(200m);
            }
        }
        catch (Exception executionError) when (databaseCreated)
        {
            cleanupAttempted = true;
            try
            {
                await DropDatabaseAsync(masterConnection.ConnectionString, databaseName);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Falló la prueba SQL y también la limpieza de su base temporal.", executionError, cleanupError);
            }

            throw;
        }
        finally
        {
            if (databaseCreated && !cleanupAttempted)
                await DropDatabaseAsync(masterConnection.ConnectionString, databaseName);
        }
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    private static void SeedCheckout(ApplicationDbContext context)
    {
        var sede = new Sede { Nombre = "Sede de prueba", Codigo = "TST", Region = "Sierra", Ciudad = "Cusco", Categoria = "Standard" };
        var tipo = new TipoHabitacion { Nombre = "Doble", CapacidadAdultos = 2, CapacidadNinos = 0 };
        var habitacion = new Habitacion
        {
            Sede = sede,
            TipoHabitacion = tipo,
            Numero = "101",
            Piso = 1,
            PrecioNoche = 100m,
            Estado = "Disponible",
            Activo = true
        };
        var persona = new Persona
        {
            TipoPersona = "N",
            Estado = "Activo",
            Natural = new PersonaNatural { Nombres = "Cliente", Apellidos = "Integración" }
        };
        var cliente = new Cliente { Persona = persona, Estado = "Activo", FechaRegistro = DateTime.UtcNow };
        persona.Cliente = cliente;
        persona.Usuario = new Usuario { Correo = "cliente@example.test", PasswordHash = "test-only-hash" };

        context.AddRange(
            sede,
            tipo,
            habitacion,
            cliente,
            new MetodoPago { Codigo = "YAPE", Nombre = "Yape", Categoria = "BilleteraDigital", Activo = true },
            new EstadoPago { Codigo = "APROBADO", Nombre = "Aprobado", EsFinal = true, Activo = true });
        context.SaveChanges();
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAANDINA_TEST_SQLSERVER")))
                Skip = "Defina CASAANDINA_TEST_SQLSERVER para ejecutar la prueba SQL Server.";
        }
    }

    private sealed class ClientUserStub(int clienteId) : ICurrentUserService
    {
        public int? UsuarioId => null;
        public int? ClienteId => clienteId;
        public int? SedeId => null;
        public RolUsuario? Rol => RolUsuario.Cliente;
    }
}
