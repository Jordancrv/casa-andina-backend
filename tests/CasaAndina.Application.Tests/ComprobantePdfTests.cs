using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ComprobantePdfTests
{
    [Fact]
    public async Task Emision_automatica_al_reservar_guarda_importes_identidad_y_pago()
    {
        await using var db = Context();
        var r = await Seed(db);
        // Reutilizar el huésped por correo evita altas ajenas al escenario del comprobante.
        var usuario = new Usuario { Id = 1, Persona = r.Cliente.Persona, Correo = "ana@example.test", PasswordHash = "test" };
        r.Cliente.Persona.Natural!.Nombres = "Ana"; r.Cliente.Persona.Natural.Apellidos = "Torres";
        db.Usuarios.Add(usuario); await db.SaveChangesAsync();
        var compra = new CrearReservaCompraCommand(1, PeruBusinessTime.Today.AddDays(10), PeruBusinessTime.Today.AddDays(12),
            [new(1, [new("Ana", "Torres", "ana@example.test", null, true)])], [], "Boleta", null, null, "YAPE");
        var response = await new CrearReservaCompraCommandHandler(db, new User(10, 1)).Handle(compra, default);
        response.ComprobanteDisponible.Should().BeTrue(); response.NumeroComprobante.Should().StartWith("B");
        var nueva = await db.Reservas.Include(x => x.Comprobante).SingleAsync(x => x.Id == response.ReservaId);
        nueva.Comprobante!.DatosEmisionJson.Should().NotBeNullOrWhiteSpace();
        var snapshot = ComprobanteEmision.Leer(nueva);
        snapshot.ClienteNombre.Should().Be("Ana Torres"); snapshot.Total.Should().Be(500);
        snapshot.Lineas.Should().ContainSingle().Which.Cantidad.Should().Be(2);
        snapshot.DetalleReconstruido.Should().BeFalse(); snapshot.PagoSimulado.Should().BeTrue();
    }

    [Fact]
    public async Task Cliente_no_puede_generar_pdf_de_reserva_ajena_ni_sin_autenticacion()
    {
        await using var db = Context(); await Seed(db);
        var foreign = () => new GenerarComprobantePdfCommandHandler(db, new User(99), new PdfStub()).Handle(new(1), default);
        await foreign.Should().ThrowAsync<NotFoundException>();
        var anonymous = () => new GenerarComprobantePdfCommandHandler(db, new User(null), new PdfStub()).Handle(new(1), default);
        await anonymous.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("RECHAZADO", 500, "PEN")]
    [InlineData("PENDIENTE", 500, "PEN")]
    [InlineData("APROBADO", 499, "PEN")]
    [InlineData("APROBADO", 500, "USD")]
    public async Task Solo_pago_aprobado_total_y_en_la_moneda_de_la_reserva_habilita_pdf(string estado, int monto, string moneda)
    {
        await using var db = Context(); await Seed(db, estado, monto, moneda);
        var act = () => new GenerarComprobantePdfCommandHandler(db, new User(10), new PdfStub()).Handle(new(1), default);
        await act.Should().ThrowAsync<ConflictException>();
        (await db.Comprobantes.SingleAsync()).DatosEmisionJson.Should().BeNull();
        (await new GetReservasQueryHandler(db, new User(10)).Handle(new(), default)).Single().ComprobanteDisponible.Should().BeFalse();
    }

    [Fact]
    public async Task Reserva_anterior_se_reconstruye_una_vez_y_no_cambia_con_el_CRM_o_catalogo()
    {
        await using var db = Context(); var r = await Seed(db);
        var pdf = new PdfStub(); var handler = new GenerarComprobantePdfCommandHandler(db, new User(10), pdf);
        var response = await handler.Handle(new(1), default);
        response.NombreArchivo.Should().Be("Boleta-B-DEMO.pdf");
        pdf.Last!.DetalleReconstruido.Should().BeTrue();
        var json = r.Comprobante!.DatosEmisionJson;
        r.Cliente.Persona.Natural!.Nombres = "Nombre cambiado";
        r.Habitaciones.Single().Habitacion.Numero = "999";
        await db.SaveChangesAsync();
        await handler.Handle(new(1), default);
        r.Comprobante.DatosEmisionJson.Should().Be(json);
        pdf.Last.ClienteNombre.Should().Be("Ana Torres");
        pdf.Last.Lineas[0].Descripcion.Should().Contain("101");
        (await db.Comprobantes.CountAsync()).Should().Be(1);
        var listado = await new GetReservasQueryHandler(db, new User(10)).Handle(new(), default);
        listado.Single().ComprobanteDisponible.Should().BeTrue(); listado.Single().TipoComprobante.Should().Be("Boleta");
    }

    [Fact]
    public async Task Factura_conserva_razon_social_y_RUC_y_no_un_nombre_comercial()
    {
        await using var db = Context(); var r = await Seed(db);
        var p = r.Cliente.Persona;
        p.TipoPersona = "J"; p.Juridica = new PersonaJuridica { RazonSocial = "Empresa de Demostración SAC", NombreComercial = "Hotel Demo" };
        var ruc = new TipoDocumento { Id = 2, Codigo = "RUC", Nombre = "RUC", TipoPersonaPermitida = "J", LongitudMinima = 11, LongitudMaxima = 11 };
        db.TiposDocumento.Add(ruc);
        db.PersonasJuridicas.Add(p.Juridica);
        p.Documentos.Add(new PersonaDocumento { TipoDocumento = ruc, TipoDocumentoId = 2, Numero = "20123456789", EsPrincipal = true });
        r.Comprobante!.Tipo = TipoComprobante.Factura;
        await db.SaveChangesAsync();
        var doc = await ComprobanteEmision.CrearAsync(db, r, false, default);
        doc.ClienteNombre.Should().Be("Empresa de Demostración SAC"); doc.DocumentoTipo.Should().Be("RUC"); doc.DocumentoNumero.Should().Be("20123456789");
    }

    [Fact]
    public async Task Suma_pagos_aprobados_y_rechaza_snapshot_invalido_o_importes_alterados()
    {
        await using var db = Context(); var r = await Seed(db, monto: 250);
        var primerPago = r.Pagos.Single().Pago;
        var segundo = new Pago { MetodoPagoId = primerPago.MetodoPagoId, MetodoPago = primerPago.MetodoPago,
            EstadoPagoId = primerPago.EstadoPagoId, EstadoPago = primerPago.EstadoPago, Importe = 250, Moneda = "PEN", FechaCreacion = DateTime.UtcNow };
        db.DetallesPago.Add(new DetallePago { Reserva = r, Pago = segundo, MontoAplicado = 250 });
        await db.SaveChangesAsync();
        await new GenerarComprobantePdfCommandHandler(db, new User(10), new PdfStub()).Handle(new(1), default);
        ComprobanteEmision.Leer(r).Pagos.Should().HaveCount(2);
        var original = r.Comprobante!.DatosEmisionJson;
        r.Comprobante.DatosEmisionJson = "{}";
        var invalid = () => new GenerarComprobantePdfCommandHandler(db, new User(10), new PdfStub()).Handle(new(1), default);
        await invalid.Should().ThrowAsync<ConflictException>();
        r.Comprobante.DatosEmisionJson = original; r.PrecioTotal = 600;
        await invalid.Should().ThrowAsync<ConflictException>();
    }

    [Theory]
    [InlineData("Boleta")][InlineData("Factura")]
    public void Genera_pdf_con_marca_y_tipografia_incluidas_sin_dependencias_de_red(string tipo)
    {
        var doc = Ejemplo(tipo);
        var result = new ComprobantePdfService().Generar(doc);
        Encoding.ASCII.GetString(result, 0, 5).Should().Be("%PDF-");
        result.Length.Should().BeGreaterThan(5000);
        var directory = Environment.GetEnvironmentVariable("CASAANDINA_PDF_QA_DIR");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, $"{tipo.ToLowerInvariant()}-ejemplo.pdf"), result);
        }
    }

    [Fact]
    public void Documento_largo_y_nombres_extensos_paginaran_sin_excepcion_de_maquetacion()
    {
        var doc = Ejemplo("Factura");
        var lineas = Enumerable.Range(1, 45).Select(i => new LineaComprobanteDto($"Servicio de demostración {i}: " + new string('W', 130), "unidad", 2, 25, 50)).ToArray();
        doc = doc with { ClienteNombre = string.Join(' ', Enumerable.Repeat("Compañía de Demostración", 8)), Lineas = lineas, Total = 2250, Pagos = [new("Tarjeta de débito", DateTime.UtcNow, 2250)] };
        var result = new ComprobantePdfService().Generar(doc);
        result.Length.Should().BeGreaterThan(5000);
        var directory = Environment.GetEnvironmentVariable("CASAANDINA_PDF_QA_TMP");
        if (directory is not null) { Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "multipagina.pdf"), result); }
    }

    public static ComprobanteDocumentoDto Ejemplo(string tipo) => new(1, tipo == "Boleta" ? "B20261026-DEMO00001" : "F20261026-DEMO00002", tipo,
        "CA-DEMO00001", new DateTime(2026, 10, 26, 14, 30, 0), "Casa Andina Select Chiclayo", "Chiclayo", "Av. Federico Villarreal 115, Chiclayo", null,
        tipo == "Boleta" ? "Ana Torres Núñez" : "Empresa de Demostración SAC", tipo == "Boleta" ? "DNI" : "RUC", tipo == "Boleta" ? "12345678" : "20123456789",
        new(2026, 10, 27), new(2026, 10, 29),
        [new("Habitación 101 - Superior King", "noche", 2, 250, 500), new("Desayuno buffet", "unidad", 2, 35, 70), new("Traslado al aeropuerto", "unidad", 1, 90, 90)],
        [new(tipo == "Boleta" ? "Yape" : "Tarjeta de débito", new(2026, 10, 26, 14, 30, 0), 660)], 660, "PEN", true, false);

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static async Task<Reserva> Seed(ApplicationDbContext db, string estado = "APROBADO", decimal monto = 500, string moneda = "PEN")
    {
        var sede = new Sede { Id = 1, Nombre = "Sede de prueba", Codigo = "TST", Ciudad = "Lima", Region = "Costa", Categoria = "Standard" };
        var persona = new Persona { Id = 10, TipoPersona = "N", Natural = new PersonaNatural { PersonaId = 10, Nombres = "Ana", Apellidos = "Torres" },
            Documentos = { new PersonaDocumento { TipoDocumento = new TipoDocumento { Id = 1, Codigo = "DNI", Nombre = "DNI", TipoPersonaPermitida = "N", LongitudMinima = 8, LongitudMaxima = 8 }, Numero = "12345678", EsPrincipal = true } } };
        var r = new Reserva { Id = 1, CodigoReserva = "CA-TEST00001", ClienteId = 10, Cliente = new Cliente { Id = 10, PersonaId = 10, Persona = persona }, SedeId = 1, Sede = sede, PrecioTotal = 500, Estado = "Confirmada",
            Comprobante = new Comprobante { NumeroComprobante = "B-DEMO", Tipo = TipoComprobante.Boleta, MontoTotal = 500, FechaEmision = DateTime.UtcNow },
            Habitaciones = { new ReservaHabitacion { Habitacion = new Habitacion { Id = 1, Sede = sede, TipoHabitacion = new TipoHabitacion { Id = 1, Nombre = "Doble", CapacidadAdultos = 2 }, Numero = "101", PrecioNoche = 250 },
                FechaCheckIn = PeruBusinessTime.Today, FechaCheckOut = PeruBusinessTime.Today.AddDays(2), PrecioAcordado = 250, Subtotal = 500, CantidadPersonas = 1 } } };
        r.Pagos.Add(new DetallePago { Pago = new Pago { MetodoPago = new MetodoPago { Codigo = "YAPE", Nombre = "Yape", Categoria = "BilleteraDigital", Activo = true },
            EstadoPago = new EstadoPago { Codigo = estado, Nombre = estado, Activo = true }, Moneda = moneda, Importe = monto, FechaCreacion = DateTime.UtcNow }, MontoAplicado = monto });
        db.Reservas.Add(r); await db.SaveChangesAsync(); return r;
    }
    private sealed class User(int? cliente, int? usuario = null) : ICurrentUserService
    { public int? UsuarioId => usuario; public int? ClienteId => cliente; public int? SedeId => null; public RolUsuario? Rol => RolUsuario.Cliente; }
    private sealed class PdfStub : IComprobantePdfService
    { public ComprobanteDocumentoDto? Last { get; private set; } public byte[] Generar(ComprobanteDocumentoDto d) { Last = d; return Encoding.ASCII.GetBytes("%PDF-test"); } }
}
