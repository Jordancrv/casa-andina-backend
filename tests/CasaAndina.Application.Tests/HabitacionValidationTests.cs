using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Habitaciones;
using CasaAndina.Application.Habitaciones.Commands;
using CasaAndina.Application.Habitaciones.Queries;
using CasaAndina.Domain.Enums;
using FluentAssertions;
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
    public void Acceso_a_habitacion_rechaza_sede_fuera_del_alcance(RolUsuario rol, int? sedeId)
    {
        var usuario = new CurrentUserStub(rol, sedeId);

        var validar = () => HabitacionRules.ValidarAccesoASede(usuario, 4);

        validar.Should().Throw<ForbiddenAccessException>();
    }

    [Theory]
    [InlineData(RolUsuario.Administrador)]
    [InlineData(RolUsuario.Superadministrador)]
    public void Administrador_global_puede_acceder_sin_sede_asignada(RolUsuario rol)
    {
        var usuario = new CurrentUserStub(rol, sedeId: null);

        var validar = () => HabitacionRules.ValidarAccesoASede(usuario, 4);

        validar.Should().NotThrow();
    }

    private sealed class CurrentUserStub(RolUsuario rol, int? sedeId) : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
        public int? SedeId => sedeId;
        public RolUsuario? Rol => rol;
    }
}
