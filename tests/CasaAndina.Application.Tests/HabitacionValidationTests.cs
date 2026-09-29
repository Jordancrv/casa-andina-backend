using CasaAndina.Application.Habitaciones.Commands;
using CasaAndina.Application.Habitaciones.Queries;
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
}
