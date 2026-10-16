using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.Queries;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class CalendarioReservasValidationTests
{
    [Fact]
    public void Rango_de_calendario_acepta_periodo_valido_incluso_historico()
    {
        var desde = new DateTime(2025, 1, 1);

        var validar = () => GetCalendarioReservasQueryHandler.ValidarRango(1, desde, desde.AddDays(30));

        validar.Should().NotThrow();
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 0)]
    [InlineData(1, 367)]
    public void Rango_de_calendario_rechaza_sede_o_fechas_invalidas(int sedeId, int dias)
    {
        var desde = new DateTime(2025, 1, 1);

        var validar = () => GetCalendarioReservasQueryHandler.ValidarRango(sedeId, desde, desde.AddDays(dias));

        validar.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData(RolUsuario.Recepcion, 8)]
    [InlineData(RolUsuario.Operaciones, null)]
    [InlineData(RolUsuario.Administrador, 8)]
    public async Task Calendario_rechaza_sede_fuera_del_alcance_del_usuario(
        RolUsuario rol, int? sedeAsignada)
    {
        await using var context = CreateContext();
        var handler = new GetCalendarioReservasQueryHandler(
            context, new CurrentUserStub(rol, sedeAsignada));

        var action = () => handler.Handle(ValidQuery(sedeId: 7), CancellationToken.None);

        await action.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Calendario_permite_consultar_sede_asignada()
    {
        await using var context = CreateContext();
        var handler = new GetCalendarioReservasQueryHandler(
            context, new CurrentUserStub(RolUsuario.Recepcion, sedeId: 7));

        var result = await handler.Handle(ValidQuery(sedeId: 7), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(RolUsuario.Administrador)]
    [InlineData(RolUsuario.Superadministrador)]
    public async Task Administrador_global_puede_consultar_sede_sin_sede_asignada(RolUsuario rol)
    {
        await using var context = CreateContext();
        var handler = new GetCalendarioReservasQueryHandler(
            context, new CurrentUserStub(rol, sedeId: null));

        var result = await handler.Handle(ValidQuery(sedeId: 7), CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static GetCalendarioReservasQuery ValidQuery(int sedeId)
        => new(sedeId, DateTime.Today, DateTime.Today.AddDays(30));

    private sealed class CurrentUserStub(RolUsuario rol, int? sedeId) : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
        public int? SedeId => sedeId;
        public RolUsuario? Rol => rol;
    }
}
