using CasaAndina.Application.Clientes.Queries;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ClientesAdminQueryTests
{
    [Fact]
    public async Task Directorio_filtra_por_sede_y_devuelve_historial_sin_exponer_contactos_adicionales()
    {
        await using var context = CreateContext();
        var hotel = new Sede { Id = 1, Nombre = "Sede prueba", Codigo = "TST", Region = "Sierra", Ciudad = "Cusco", Categoria = "Standard" };
        var persona = new Persona
        {
            Id = 10,
            TipoPersona = "N",
            Natural = new PersonaNatural { PersonaId = 10, Nombres = "Ana", Apellidos = "Prueba" },
            Contactos = { new PersonaContacto { TipoContacto = "Correo", Valor = "ana@example.test", EsPrincipal = true } }
        };
        var user = new Usuario
        {
            Id = 40,
            PersonaId = persona.Id,
            Persona = persona,
            Correo = "ana.access@example.test",
            PasswordHash = "test-hash"
        };
        persona.Usuario = user;
        var cliente = new Cliente { Id = 20, PersonaId = persona.Id, Persona = persona, Estado = "Activo" };
        var reserva = new Reserva
        {
            Id = 30, CodigoReserva = "CA-TEST-30", ClienteId = cliente.Id, Cliente = cliente,
            SedeId = hotel.Id, Sede = hotel, Estado = "Reservado", PrecioTotal = 250,
            Habitaciones = { new ReservaHabitacion { FechaCheckIn = new DateTime(2026, 12, 1), FechaCheckOut = new DateTime(2026, 12, 3) } }
        };
        var otherHotel = new Sede { Id = 2, Nombre = "Otra sede", Codigo = "OTH", Region = "Costa", Ciudad = "Lima", Categoria = "Standard" };
        var foreignReservation = new Reserva
        {
            Id = 31, CodigoReserva = "CA-TEST-31", ClienteId = cliente.Id, Cliente = cliente,
            SedeId = otherHotel.Id, Sede = otherHotel, Estado = "Reservado", PrecioTotal = 500
        };
        var approved = new EstadoPago { Id = 1, Codigo = "APROBADO", Nombre = "Aprobado", Activo = true };
        var rejected = new EstadoPago { Id = 2, Codigo = "RECHAZADO", Nombre = "Rechazado", Activo = true };
        var method = new MetodoPago
        {
            Id = 1, Codigo = "YAPE", Nombre = "Yape", Categoria = "BilleteraDigital",
            Activo = true, FechaCreacion = DateTime.UtcNow
        };
        var payments = new[]
        {
            new DetallePago
            {
                Id = 50, ReservaId = reserva.Id, Reserva = reserva, PagoId = 60,
                Pago = new Pago { Id = 60, MetodoPagoId = method.Id, MetodoPago = method,
                    EstadoPagoId = approved.Id, EstadoPago = approved, Importe = 180,
                    Moneda = "PEN", FechaCreacion = DateTime.UtcNow },
                MontoAplicado = 180
            },
            new DetallePago
            {
                Id = 51, ReservaId = reserva.Id, Reserva = reserva, PagoId = 61,
                Pago = new Pago { Id = 61, MetodoPagoId = method.Id, MetodoPago = method,
                    EstadoPagoId = rejected.Id, EstadoPago = rejected, Importe = 70,
                    Moneda = "PEN", FechaCreacion = DateTime.UtcNow },
                MontoAplicado = 70
            },
            new DetallePago
            {
                Id = 52, ReservaId = foreignReservation.Id, Reserva = foreignReservation, PagoId = 62,
                Pago = new Pago { Id = 62, MetodoPagoId = method.Id, MetodoPago = method,
                    EstadoPagoId = approved.Id, EstadoPago = approved, Importe = 500,
                    Moneda = "PEN", FechaCreacion = DateTime.UtcNow },
                MontoAplicado = 500
            }
        };
        reserva.Pagos.Add(payments[0]);
        reserva.Pagos.Add(payments[1]);
        foreignReservation.Pagos.Add(payments[2]);
        context.Clientes.Add(cliente);
        context.Usuarios.Add(user);
        context.Sedes.Add(otherHotel);
        context.Reservas.AddRange(reserva, foreignReservation);
        context.EstadosPago.AddRange(approved, rejected);
        context.MetodosPago.Add(method);
        context.DetallesPago.AddRange(payments);
        await context.SaveChangesAsync();
        var handler = new GetClientesAdminQueryHandler(context, new CurrentUserStub(RolUsuario.Administrador, hotel.Id));

        var result = await handler.Handle(new GetClientesAdminQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        var item = result.Items.Single();
        item.Nombre.Should().Be("Ana Prueba");
        item.TotalReservas.Should().Be(1);
        item.ImporteReservado.Should().Be(250);
        item.ImportePagadoSimulado.Should().Be(180);
        item.CorreoAcceso.Should().Be("ana.access@example.test");
        item.CorreoNotificacion.Should().Be("ana@example.test");
        item.Historial.Should().ContainSingle().Which.CodigoReserva.Should().Be("CA-TEST-30");

        var foundByLoginEmail = await handler.Handle(
            new GetClientesAdminQuery("ana.access@example.test"), CancellationToken.None);
        foundByLoginEmail.Items.Should().ContainSingle().Which.ClienteId.Should().Be(cliente.Id);
    }

    [Fact]
    public async Task Directorio_rechaza_roles_sin_permiso_administrativo()
    {
        await using var context = CreateContext();
        var handler = new GetClientesAdminQueryHandler(context, new CurrentUserStub(RolUsuario.Recepcion, 1));

        var action = () => handler.Handle(new GetClientesAdminQuery(), CancellationToken.None);

        await action.Should().ThrowAsync<CasaAndina.Application.Common.Exceptions.ForbiddenAccessException>();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private sealed class CurrentUserStub(RolUsuario role, int? sedeId) : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
        public int? SedeId => sedeId;
        public RolUsuario? Rol => role;
    }
}
