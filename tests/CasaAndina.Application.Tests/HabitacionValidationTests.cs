using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Habitaciones;
using CasaAndina.Application.Habitaciones.Commands;
using CasaAndina.Application.Habitaciones.Queries;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public class HabitacionValidationTests
{
    [Fact]
    public async Task Crear_deberia_rechazar_valores_fuera_del_esquema_sql()
    {
        var command = new CreateHabitacionCommand(
            1, 1, "101", 256, 100.123m, "Disponible", null, []);

        var result = await new CreateHabitacionCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(command.Piso));
        result.Errors.Should().Contain(x => x.PropertyName == nameof(command.PrecioBase));
    }

    [Fact]
    public async Task Crear_y_actualizar_deberian_rechazar_numero_de_habitacion_en_blanco()
    {
        var create = await new CreateHabitacionCommandValidator().ValidateAsync(
            new CreateHabitacionCommand(1, 1, "   ", 1, 100m, "Disponible", null, []));
        var update = await new UpdateHabitacionCommandValidator().ValidateAsync(new UpdateHabitacionCommand
        {
            Id = 1, SedeId = 1, TipoHabitacionId = 1, Numero = "  ", Piso = 1,
            PrecioBase = 100m, Estado = "Disponible", ComodidadesIds = []
        });

        create.Errors.Should().Contain(x => x.PropertyName == nameof(CreateHabitacionCommand.Numero));
        update.Errors.Should().Contain(x => x.PropertyName == nameof(UpdateHabitacionCommand.Numero));
    }

    [Fact]
    public async Task Listado_deberia_limitar_el_tamano_de_pagina()
    {
        var query = new GetHabitacionesQuery(null, null, 0, 101);

        var result = await new GetHabitacionesQueryValidator().ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(query.PageNumber));
        result.Errors.Should().Contain(x => x.PropertyName == nameof(query.PageSize));
    }

    [Fact]
    public void Acceso_a_habitacion_permite_sede_asignada()
    {
        var usuario = new CurrentUserStub(RolUsuario.Recepcion, sedeId: 4);

        var validar = () => HabitacionRules.ValidarAccesoASede(usuario, 4);

        validar.Should().NotThrow();
    }

    [Theory]
    [InlineData(RolUsuario.Recepcion, 5)]
    [InlineData(RolUsuario.Operaciones, null)]
    [InlineData(RolUsuario.Administrador, null)]
    public void Acceso_a_habitacion_rechaza_sede_fuera_del_alcance(RolUsuario rol, int? sedeId)
    {
        var usuario = new CurrentUserStub(rol, sedeId);

        var validar = () => HabitacionRules.ValidarAccesoASede(usuario, 4);

        validar.Should().Throw<ForbiddenAccessException>();
    }

    [Fact]
    public void Superadministrador_global_puede_acceder_sin_sede_asignada()
    {
        var usuario = new CurrentUserStub(RolUsuario.Superadministrador, sedeId: null);

        var validar = () => HabitacionRules.ValidarAccesoASede(usuario, 4);

        validar.Should().NotThrow();
    }

    [Fact]
    public async Task Listado_rechaza_administrador_sin_sede()
    {
        await using var context = CreateContext();
        var handler = new GetHabitacionesQueryHandler(
            context, new CurrentUserStub(RolUsuario.Administrador, null));

        var action = () => handler.Handle(new GetHabitacionesQuery(null, null), CancellationToken.None);

        await action.Should().ThrowAsync<ForbiddenAccessException>();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private sealed class CurrentUserStub(RolUsuario rol, int? sedeId) : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
        public int? SedeId => sedeId;
        public RolUsuario? Rol => rol;
    }
}
