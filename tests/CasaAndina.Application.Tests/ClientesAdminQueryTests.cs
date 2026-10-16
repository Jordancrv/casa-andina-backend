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
        var cliente = new Cliente { Id = 20, PersonaId = persona.Id, Persona = persona, Estado = "Activo" };
        var reserva = new Reserva
        {
            Id = 30, CodigoReserva = "CA-TEST-30", ClienteId = cliente.Id, Cliente = cliente,
            SedeId = hotel.Id, Sede = hotel, Estado = "Confirmada", PrecioTotal = 250,
            Habitaciones = { new ReservaHabitacion { FechaCheckIn = new DateTime(2026, 12, 1), FechaCheckOut = new DateTime(2026, 12, 3) } }
        };
        context.Clientes.Add(cliente);
        context.Reservas.Add(reserva);
        await context.SaveChangesAsync();
        var handler = new GetClientesAdminQueryHandler(context, new CurrentUserStub(RolUsuario.Administrador, hotel.Id));

        var result = await handler.Handle(new GetClientesAdminQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        var item = result.Items.Single();
        item.Nombre.Should().Be("Ana Prueba");
        item.TotalReservas.Should().Be(1);
        item.ImporteReservado.Should().Be(250);
        item.Correo.Should().Be("ana@example.test");
        item.Historial.Should().ContainSingle().Which.CodigoReserva.Should().Be("CA-TEST-30");
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
