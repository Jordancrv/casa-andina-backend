using CasaAndina.Application.Clientes.Queries;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ClientesAdminQuerySqlServerTests
{
    [SqlServerFact]
    public async Task Directorio_traduce_y_filtra_pagos_simulados_en_SQL_Server()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("CASAANDINA_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
            throw new InvalidOperationException("La prueba SQL Server requiere CASAANDINA_TEST_SQLSERVER.");

        var databaseName = $"CasaAndinaTestCrm_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName,
            MultipleActiveResultSets = true
        };
        databaseName.Should().StartWith("CasaAndinaTestCrm_");
        databaseName.Should().NotBe("CasaAndinaDb");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;
        await using var context = new ApplicationDbContext(options);
        var databaseCreated = false;

        try
        {
            databaseCreated = await context.Database.EnsureCreatedAsync();
            databaseCreated.Should().BeTrue("el nombre aleatorio debe crear una base aislada para esta prueba");

            var sede = NewSede("Sede de prueba", "TST");
            var otraSede = NewSede("Otra sede", "OTH");
            var tipoHabitacion = new TipoHabitacion { Nombre = "Doble" };
            var habitacion = new Habitacion
            {
                Sede = sede,
                TipoHabitacion = tipoHabitacion,
                Numero = "101",
                Piso = 1,
                PrecioNoche = 200,
                Estado = "Disponible"
            };
            var persona = new Persona
            {
                TipoPersona = "N",
                Estado = "Activo",
                Natural = new PersonaNatural { Nombres = "Cliente", Apellidos = "Prueba" },
                Contactos = { new PersonaContacto { TipoContacto = "Correo", Valor = "crm@example.test", EsPrincipal = true } }
            };
            var cliente = new Cliente { Persona = persona, Estado = "Activo", FechaRegistro = DateTime.UtcNow };
            var reserva = NewReserva(cliente, sede, "CA-SQL-1", 250);
            reserva.Habitaciones.Add(new ReservaHabitacion
            {
                Habitacion = habitacion,
                FechaCheckIn = new DateTime(2026, 12, 1),
                FechaCheckOut = new DateTime(2026, 12, 3),
                CantidadPersonas = 1,
                PrecioAcordado = 250,
                Subtotal = 250
            });
            var reservaOtraSede = NewReserva(cliente, otraSede, "CA-SQL-2", 500);
            var metodo = new MetodoPago
            {
                Codigo = "YAPE", Nombre = "Yape", Categoria = "BilleteraDigital", Activo = true
            };
            var aprobado = new EstadoPago { Codigo = "APROBADO", Nombre = "Aprobado", Activo = true };
            var rechazado = new EstadoPago { Codigo = "RECHAZADO", Nombre = "Rechazado", Activo = true };

            AddPayment(reserva, metodo, aprobado, 180);
            AddPayment(reserva, metodo, rechazado, 70);
            AddPayment(reservaOtraSede, metodo, aprobado, 500);

            context.AddRange(sede, otraSede, tipoHabitacion, habitacion, cliente, reserva,
                reservaOtraSede, metodo, aprobado, rechazado);
            await context.SaveChangesAsync();

            var handler = new GetClientesAdminQueryHandler(context, new CurrentUserStub(sede.Id));
            var result = await handler.Handle(new GetClientesAdminQuery(), CancellationToken.None);

            result.TotalCount.Should().Be(1);
            var item = result.Items.Should().ContainSingle().Subject;
            item.TotalReservas.Should().Be(1);
            item.ImporteReservado.Should().Be(250);
            item.ImportePagadoSimulado.Should().Be(180);
            item.Historial.Should().ContainSingle().Which.CodigoReserva.Should().Be("CA-SQL-1");
        }
        finally
        {
            if (databaseCreated)
                await context.Database.EnsureDeletedAsync();
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAANDINA_TEST_SQLSERVER")))
                Skip = "Defina CASAANDINA_TEST_SQLSERVER para ejecutar la prueba SQL Server.";
        }
    }

    private static Sede NewSede(string nombre, string codigo) => new()
    {
        Nombre = nombre, Codigo = codigo, Region = "Sierra", Ciudad = "Cusco", Categoria = "Standard"
    };

    private static Reserva NewReserva(Cliente cliente, Sede sede, string codigo, decimal total) => new()
    {
        Cliente = cliente,
        Sede = sede,
        CodigoReserva = codigo,
        Estado = "Confirmada",
        Canal = "Directo",
        PrecioTotal = total
    };

    private static void AddPayment(Reserva reserva, MetodoPago metodo, EstadoPago estado, decimal monto)
    {
        var pago = new Pago
        {
            MetodoPago = metodo,
            EstadoPago = estado,
            Importe = monto,
            Moneda = "PEN"
        };
        reserva.Pagos.Add(new DetallePago { Pago = pago, MontoAplicado = monto });
    }

    private sealed class CurrentUserStub(int sedeId) : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
        public int? SedeId => sedeId;
        public RolUsuario? Rol => RolUsuario.Administrador;
    }
}
