using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas;
using CasaAndina.Application.Reservas.Commands;
using CasaAndina.Application.Reservas.DTOs;
using CasaAndina.Application.Reservas.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ComprobantePdfSqlServerTests
{
    [SqlServerFact] public Task Boleta_pagada_se_emite_y_se_consulta_desde_SQL() => VerificarCompra("Boleta");
    [SqlServerFact] public Task Factura_pagada_conserva_RUC_y_razon_social_desde_SQL() => VerificarCompra("Factura");

    private static async Task VerificarCompra(string tipo)
    {
        var connection = Conexion();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection.ConnectionString).Options);
        var created = false;
        try
        {
            created = await db.Database.EnsureCreatedAsync(); created.Should().BeTrue();
            var sede = new Sede { Nombre = "Sede PDF", Codigo = "PDF", Region = "Costa", Ciudad = "Lima", Categoria = "Standard" };
            var persona = new Persona { TipoPersona = tipo == "Boleta" ? "N" : "J", FechaRegistro = DateTime.UtcNow };
            if (tipo == "Boleta") persona.Natural = new PersonaNatural { Nombres = "Ana", Apellidos = "Torres" };
            else persona.Juridica = new PersonaJuridica { RazonSocial = "Empresa de Demostración SAC", NombreComercial = "Empresa Demo" };
            var doc = new TipoDocumento { Codigo = tipo == "Boleta" ? "DNI" : "RUC", Nombre = "Documento", TipoPersonaPermitida = persona.TipoPersona,
                LongitudMinima = (byte)(tipo == "Boleta" ? 8 : 11), LongitudMaxima = (byte)(tipo == "Boleta" ? 8 : 11) };
            persona.Documentos.Add(new PersonaDocumento { TipoDocumento = doc, Numero = tipo == "Boleta" ? "12345678" : "20123456789", EsPrincipal = true });
            var cliente = new Cliente { Persona = persona, FechaRegistro = DateTime.UtcNow };
            var usuario = new Usuario { Persona = persona, Correo = "titular@example.test", PasswordHash = "test" };
            var room = new Habitacion { Sede = sede, TipoHabitacion = new TipoHabitacion { Nombre = "Doble", CapacidadAdultos = 2 }, Numero = "101", Piso = 1, PrecioNoche = 350 };
            var servicio = new Servicio { Nombre = "Desayuno", Precio = 35, Sedes = { sede } };
            db.AddRange(cliente, usuario, room, servicio,
                new MetodoPago { Codigo = "YAPE", Nombre = "Yape", Categoria = "BilleteraDigital", Activo = true },
                new EstadoPago { Codigo = "APROBADO", Nombre = "Aprobado", Activo = true });
            await db.SaveChangesAsync();
            var user = new User(cliente.Id, usuario.Id);
            var compra = new CrearReservaCompraCommand(sede.Id, PeruBusinessTime.Today.AddDays(4), PeruBusinessTime.Today.AddDays(6),
                [new(room.Id, [new("Huésped", "Prueba", "visita@example.test", null, true)])], [new(servicio.Id, 2)],
                tipo, tipo == "Factura" ? persona.Juridica!.RazonSocial : null, tipo == "Factura" ? "20123456789" : null, "YAPE");
            var response = await new CrearReservaCompraCommandHandler(db, user).Handle(compra, default);
            response.ComprobanteDisponible.Should().BeTrue();
            db.ChangeTracker.Clear();
            var listado = await new GetReservasQueryHandler(db, user).Handle(new(), default);
            listado.Should().ContainSingle().Which.ComprobanteDisponible.Should().BeTrue();
            listado.Single().TipoComprobante.Should().Be(tipo);
            var reserva = await db.Reservas.Include(r => r.Comprobante).SingleAsync();
            var datos = ComprobanteEmision.Leer(reserva);
            datos.Total.Should().Be(770); datos.Lineas.Should().HaveCount(2); datos.DetalleReconstruido.Should().BeFalse();
            datos.DocumentoNumero.Should().Be(tipo == "Boleta" ? "12345678" : "20123456789");
            var copia = reserva.Comprobante!.DatosEmisionJson;
            var pdf = await new GenerarComprobantePdfCommandHandler(db, user, new ComprobantePdfService()).Handle(new(reserva.Id), default);
            Encoding.ASCII.GetString(pdf.Contenido, 0, 5).Should().Be("%PDF-");
            pdf.NombreArchivo.Should().StartWith(tipo).And.EndWith(".pdf");
            reserva.Comprobante.DatosEmisionJson.Should().Be(copia);
            (await db.Comprobantes.CountAsync()).Should().Be(1);
        }
        finally { if (created) await db.Database.EnsureDeletedAsync(); }
    }

    [SqlServerFact]
    public async Task Migracion_agrega_snapshot_sin_perder_comprobantes_previos_y_es_idempotente()
    {
        var connection = Conexion();
        await using var master = new SqlConnection(new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" }.ConnectionString);
        await master.OpenAsync(); var created = false;
        try
        {
            await new SqlCommand($"CREATE DATABASE [{connection.InitialCatalog}]", master).ExecuteNonQueryAsync(); created = true;
            await using var target = new SqlConnection(connection.ConnectionString); await target.OpenAsync();
            await new SqlCommand("CREATE TABLE dbo.Comprobante(ComprobanteId int PRIMARY KEY); INSERT dbo.Comprobante VALUES(1);", target).ExecuteNonQueryAsync();
            var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../scripts/migrate-comprobante-pdf.sql"));
            var script = await File.ReadAllTextAsync(path);
            await new SqlCommand(script, target).ExecuteNonQueryAsync(); await new SqlCommand(script, target).ExecuteNonQueryAsync();
            (await new SqlCommand("SELECT COUNT(*) FROM dbo.Comprobante WHERE ComprobanteId=1 AND DatosEmisionJson IS NULL", target).ExecuteScalarAsync()).Should().Be(1);
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

    private static SqlConnectionStringBuilder Conexion()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CASAANDINA_TEST_SQLSERVER"))
        { InitialCatalog = $"CasaAndinaTestPdf_{Guid.NewGuid():N}", MultipleActiveResultSets = true };
        connection.InitialCatalog.Should().MatchRegex("^CasaAndinaTestPdf_[a-f0-9]{32}$");
        return connection;
    }
    private sealed class User(int cliente, int usuario) : ICurrentUserService
    { public int? UsuarioId => usuario; public int? ClienteId => cliente; public int? SedeId => null; public RolUsuario? Rol => RolUsuario.Cliente; }
    private sealed class SqlServerFactAttribute : FactAttribute
    { public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAANDINA_TEST_SQLSERVER"))) Skip = "Defina CASAANDINA_TEST_SQLSERVER para pruebas SQL aisladas."; } }
}
