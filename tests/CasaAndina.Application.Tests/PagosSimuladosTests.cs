using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Reservas.Commands;
using CasaAndina.Application.Reservas.DTOs;
using CasaAndina.Application.Reservas.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class PagosSimuladosTests
{
    [Fact]
    public async Task Mapeo_EF_coincide_con_las_columnas_principales_del_esquema()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=CasaAndinaMetadataOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        await using var context = new ApplicationDbContext(options);
        var payment = context.Model.FindEntityType(typeof(Pago))!;
        var detail = context.Model.FindEntityType(typeof(DetallePago))!;
        var method = context.Model.FindEntityType(typeof(MetodoPago))!;
        var state = context.Model.FindEntityType(typeof(EstadoPago))!;
        var reservation = context.Model.FindEntityType(typeof(Reserva))!;
        reservation.FindProperty(nameof(Reserva.Activo)).Should().BeNull();
        reservation.FindProperty(nameof(Reserva.FechaActualizacion)).Should().BeNull();
        var paymentTable = StoreObjectIdentifier.Table("Pago", payment.GetSchema());
        var detailTable = StoreObjectIdentifier.Table("DetallePago", detail.GetSchema());

        payment.GetTableName().Should().Be("Pago");
        payment.FindProperty(nameof(Pago.Id))!.GetColumnName(paymentTable).Should().Be("PagoId");
        payment.FindProperty(nameof(Pago.Importe))!.GetColumnType().Should().Be("decimal(10,2)");
        payment.FindProperty(nameof(Pago.ReferenciaProveedor))!.GetMaxLength().Should().Be(100);
        method.FindProperty(nameof(MetodoPago.Codigo))!.GetMaxLength().Should().Be(30);
        method.FindProperty(nameof(MetodoPago.Nombre))!.GetMaxLength().Should().Be(80);
        state.FindProperty(nameof(EstadoPago.Codigo))!.GetMaxLength().Should().Be(30);
        state.FindProperty(nameof(EstadoPago.Nombre))!.GetMaxLength().Should().Be(60);
        detail.GetTableName().Should().Be("DetallePago");
        detail.FindProperty(nameof(DetallePago.Id))!.GetColumnName(detailTable).Should().Be("DetallePagoId");
        detail.FindProperty(nameof(DetallePago.MontoAplicado))!.GetColumnType().Should().Be("decimal(10,2)");
        detail.GetForeignKeys().Should().HaveCount(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("                                                                 ")]
    public async Task Compra_requiere_codigo_de_metodo_de_pago(string codigo)
    {
        var command = ValidCommand(codigo);

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(command.MetodoPagoCodigo));
    }

    [Fact]
    public async Task Compra_registra_metodo_estado_y_monto_simulados_sin_referencia_de_proveedor()
    {
        await using var context = CreateContext();
        SeedCheckout(context);
        var handler = new CrearReservaCompraCommandHandler(context, new CurrentUserStub(10));
        var command = new CrearReservaCompraCommand(
            1,
            DateTime.Today.AddDays(2),
            DateTime.Today.AddDays(3),
            [new HabitacionCompraDto(20, [new HuespedCompraDto("Huésped", "Prueba", "guest@example.test", null, true, "12345678", new DateTime(1990, 1, 1), "F")])],
            [],
            "Boleta",
            null,
            null,
            "YAPE");

        var result = await handler.Handle(command, CancellationToken.None);

        var payment = await context.Pagos.Include(x => x.MetodoPago)
            .Include(x => x.EstadoPago).Include(x => x.Detalles)
            .SingleAsync();
        payment.MetodoPago.Codigo.Should().Be("YAPE");
        payment.EstadoPago.Codigo.Should().Be("APROBADO");
        payment.Importe.Should().Be(150m);
        payment.Moneda.Should().Be("PEN");
        payment.ReferenciaProveedor.Should().BeNull();
        payment.Detalles.Should().ContainSingle().Which.MontoAplicado.Should().Be(150m);
        payment.Detalles.Single().ReservaId.Should().Be(result.ReservaId);
        result.MetodoPago.Should().Be("Yape");
        result.EstadoPago.Should().Be("Aprobado");
        result.MensajePago.Should().Contain("No se realizó ningún cobro real");
    }

    [Fact]
    public async Task Catalogo_de_pago_devuelve_solo_metodos_activos()
    {
        await using var context = CreateContext();
        context.MetodosPago.AddRange(
            new MetodoPago { Id = 1, Codigo = "YAPE", Nombre = "Yape", Categoria = "BilleteraDigital", Activo = true },
            new MetodoPago { Id = 2, Codigo = "OTRO", Nombre = "Descontinuado", Categoria = "Otro", Activo = false });
        await context.SaveChangesAsync();

        var result = await new GetMetodosPagoQueryHandler(context)
            .Handle(new GetMetodosPagoQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.Codigo.Should().Be("YAPE");
    }

    [Fact]
    public async Task Compra_rechaza_metodo_inactivo_sin_persistir_reserva_ni_pago()
    {
        await using var context = CreateContext();
        SeedCheckout(context);
        context.MetodosPago.Single().Activo = false;
        await context.SaveChangesAsync();
        var handler = new CrearReservaCompraCommandHandler(context, new CurrentUserStub(10));

        var action = () => handler.Handle(ValidCommand("YAPE"), CancellationToken.None);

        await action.Should().ThrowAsync<ValidationException>();
        (await context.Reservas.CountAsync()).Should().Be(0);
        (await context.Pagos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Compra_rechaza_total_cero_sin_crear_pago()
    {
        await using var context = CreateContext();
        SeedCheckout(context);
        context.Habitaciones.Single().PrecioNoche = 0;
        await context.SaveChangesAsync();
        var handler = new CrearReservaCompraCommandHandler(context, new CurrentUserStub(10));

        var action = () => handler.Handle(ValidCommand("YAPE"), CancellationToken.None);

        await action.Should().ThrowAsync<ValidationException>();
        (await context.Reservas.CountAsync()).Should().Be(0);
        (await context.Pagos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Mis_reservas_incluye_el_estado_y_metodo_solo_de_las_reservas_del_cliente()
    {
        await using var context = CreateContext();
        SeedReservationsWithPayment(context);

        var result = await new GetReservasQueryHandler(context, new CurrentUserStub(10))
            .Handle(new GetReservasQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Pagos.Should().ContainSingle();
        result[0].Pagos[0].MetodoPago.Should().Be("Yape");
        result[0].Pagos[0].Estado.Should().Be("Aprobado");
        result[0].Pagos[0].MontoAplicado.Should().Be(100m);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static CrearReservaCompraCommand ValidCommand(string methodCode) => new(
        1,
        DateTime.Today.AddDays(2),
        DateTime.Today.AddDays(3),
        [new HabitacionCompraDto(20, [new HuespedCompraDto("Huésped", "Prueba", "guest@example.test", null, true, "12345678", new DateTime(1990, 1, 1), "F")])],
        [],
        "Boleta",
        null,
        null,
        methodCode);

    private static void SeedCheckout(ApplicationDbContext context)
    {
        var sede = new Sede { Id = 1, Nombre = "Sede de prueba", Codigo = "TST", Region = "Sierra", Ciudad = "Cusco", Categoria = "Standard" };
        var type = new TipoHabitacion { Id = 1, Nombre = "Doble", CapacidadAdultos = 2 };
        var room = new Habitacion
        {
            Id = 20, SedeId = sede.Id, Sede = sede, TipoHabitacionId = type.Id, TipoHabitacion = type,
            Numero = "201", Piso = 2, PrecioNoche = 150, Estado = "Disponible", Activo = true
        };
        var person = new Persona { Id = 100, TipoPersona = "N", Estado = "Activo", FechaRegistro = DateTime.Today,
            Natural = new PersonaNatural { PersonaId = 100, Nombres = "Cliente", Apellidos = "Prueba" } };
        var client = new Cliente { Id = 10, PersonaId = person.Id, Persona = person, Estado = "Activo" };
        person.Cliente = client;
        context.Sedes.Add(sede);
        context.TiposHabitacion.Add(type);
        context.Habitaciones.Add(room);
        context.Personas.Add(person);
        context.Clientes.Add(client);
        context.MetodosPago.Add(new MetodoPago { Id = 1, Codigo = "YAPE", Nombre = "Yape", Categoria = "BilleteraDigital", Activo = true });
        context.EstadosPago.Add(new EstadoPago { Id = 1, Codigo = "APROBADO", Nombre = "Aprobado", EsFinal = true, Activo = true });
        context.SaveChanges();
    }

    private static void SeedReservationsWithPayment(ApplicationDbContext context)
    {
        var sede = new Sede { Id = 1, Nombre = "Sede de prueba", Codigo = "TST", Region = "Sierra", Ciudad = "Cusco", Categoria = "Standard" };
        var type = new TipoHabitacion { Id = 1, Nombre = "Doble" };
        var room = new Habitacion { Id = 20, SedeId = 1, Sede = sede, TipoHabitacionId = 1, TipoHabitacion = type, Numero = "201", Piso = 2 };
        var person = new Persona { Id = 100, TipoPersona = "N", Natural = new PersonaNatural { Nombres = "Cliente", Apellidos = "10" } };
        var client = new Cliente { Id = 10, PersonaId = person.Id, Persona = person };
        person.Cliente = client;
        var reservation = new Reserva
        {
            Id = 100, CodigoReserva = "CA-CLIENTE-10", ClienteId = client.Id, Cliente = client,
            SedeId = sede.Id, Sede = sede, PrecioTotal = 100
        };
        reservation.Habitaciones.Add(new ReservaHabitacion
        {
            Id = 1100, ReservaId = reservation.Id, Reserva = reservation, HabitacionId = room.Id, Habitacion = room,
            FechaCheckIn = DateTime.Today.AddDays(2), FechaCheckOut = DateTime.Today.AddDays(3),
            CantidadPersonas = 1, PrecioAcordado = 100, Subtotal = 100
        });
        var method = new MetodoPago { Id = 1, Codigo = "YAPE", Nombre = "Yape", Categoria = "BilleteraDigital", Activo = true };
        var state = new EstadoPago { Id = 1, Codigo = "APROBADO", Nombre = "Aprobado", EsFinal = true, Activo = true };
        var payment = new Pago { Id = 1, MetodoPagoId = 1, MetodoPago = method, EstadoPagoId = 1, EstadoPago = state, Importe = 100, Moneda = "PEN" };
        reservation.Pagos.Add(new DetallePago
        {
            Id = 1, ReservaId = reservation.Id, Reserva = reservation, PagoId = payment.Id, Pago = payment,
            MontoAplicado = 100, FechaAplicacion = DateTime.Today
        });
        var otherClient = new Cliente
        {
            Id = 11, PersonaId = 101,
            Persona = new Persona { Id = 101, TipoPersona = "N", Natural = new PersonaNatural { Nombres = "Cliente", Apellidos = "11" } }
        };
        var otherReservation = new Reserva
        {
            Id = 101, CodigoReserva = "CA-CLIENTE-11", ClienteId = otherClient.Id, Cliente = otherClient,
            SedeId = sede.Id, Sede = sede, PrecioTotal = 100
        };
        otherReservation.Habitaciones.Add(new ReservaHabitacion
        {
            Id = 1101, ReservaId = otherReservation.Id, Reserva = otherReservation, HabitacionId = room.Id, Habitacion = room,
            FechaCheckIn = DateTime.Today.AddDays(4), FechaCheckOut = DateTime.Today.AddDays(5),
            CantidadPersonas = 1, PrecioAcordado = 100, Subtotal = 100
        });
        var otherPayment = new Pago
        {
            Id = 2, MetodoPagoId = 1, MetodoPago = method, EstadoPagoId = 1, EstadoPago = state,
            Importe = 100, Moneda = "PEN"
        };
        otherReservation.Pagos.Add(new DetallePago
        {
            Id = 2, ReservaId = otherReservation.Id, Reserva = otherReservation, PagoId = otherPayment.Id,
            Pago = otherPayment, MontoAplicado = 100, FechaAplicacion = DateTime.Today
        });
        context.Reservas.AddRange(reservation, otherReservation);
        context.Pagos.AddRange(payment, otherPayment);
        context.SaveChanges();
    }

    private sealed class CurrentUserStub(int? clienteId) : ICurrentUserService
    {
        public int? UsuarioId => clienteId;
        public int? ClienteId => clienteId;
        public int? SedeId => null;
        public RolUsuario? Rol => clienteId.HasValue ? RolUsuario.Cliente : null;
    }
}
