using CasaAndina.Application.Common.Interfaces;
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
        var connection = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName,
            MultipleActiveResultSets = true
        };
        databaseName.Should().StartWith("CasaAndinaTestCompra_");
        databaseName.Should().NotBe("CasaAndinaDb");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;
        var databaseCreated = false;

        try
        {
            await using (var seedContext = new ApplicationDbContext(options))
            {
                databaseCreated = await seedContext.Database.EnsureCreatedAsync();
                databaseCreated.Should().BeTrue("cada ejecución utiliza una base con nombre aleatorio");
                SeedCheckout(seedContext);
            }

            ReservaCompraResponseDto result;
            await using (var writeContext = new ApplicationDbContext(options))
            {
                var handler = new CrearReservaCompraCommandHandler(writeContext, new ClientUserStub(1));
                result = await handler.Handle(new CrearReservaCompraCommand(
                    1,
                    DateTime.Today.AddDays(2),
                    DateTime.Today.AddDays(4),
                    [new HabitacionCompraDto(1, [new HuespedCompraDto("Huésped", "Prueba", "guest@example.test", null, true)])],
                    [],
                    "Boleta",
                    null,
                    null,
                    "YAPE"),
                    CancellationToken.None);
            }

            await using var verifyContext = new ApplicationDbContext(options);
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
            reservation.Habitaciones.Single().Huespedes.Should().ContainSingle()
                .Which.Huesped.PersonaNatural.Nombres.Should().Be("Huésped");
            reservation.Comprobante.Should().NotBeNull();
            reservation.Comprobante!.Tipo.Should().Be(TipoComprobante.Boleta);
            payment.MetodoPago.Codigo.Should().Be("YAPE");
            payment.EstadoPago.Codigo.Should().Be("APROBADO");
            payment.Importe.Should().Be(200m);
            payment.ReferenciaProveedor.Should().BeNull();
            payment.Detalles.Should().ContainSingle().Which.MontoAplicado.Should().Be(200m);
        }
        finally
        {
            if (databaseCreated)
            {
                await using var cleanupContext = new ApplicationDbContext(options);
                await cleanupContext.Database.EnsureDeletedAsync();
            }
        }
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
