using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Servicios;
using CasaAndina.Application.Servicios.Commands;
using CasaAndina.Application.Servicios.Queries;
using CasaAndina.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CasaAndina.Application.Tests;

public class ServicioValidationTests
{
    [Fact]
    public async Task Crear_deberia_rechazar_precio_y_sedes_invalidas()
    {
        var command = new CreateServicioCommand("Traslado", null, 10.123m, []);

        var result = await new CreateServicioCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(command.Precio));
        result.Errors.Should().Contain(x => x.PropertyName == nameof(command.SedesIds));
    }

    [Fact]
    public async Task Listado_deberia_validar_paginacion()
    {
        var query = new GetServiciosQuery(null, null, 0, 101);

        var result = await new GetServiciosQueryValidator().ValidateAsync(query);

        result.Errors.Should().Contain(x => x.PropertyName == nameof(query.PageNumber));
        result.Errors.Should().Contain(x => x.PropertyName == nameof(query.PageSize));
    }

    [Fact]
    public void Operaciones_no_deberia_gestionar_servicios_de_otra_sede()
    {
        var currentUser = new CurrentUserStub(RolUsuario.Operaciones, 2);

        var action = () => ServicioRules.ValidarSedes(currentUser, [1]);

        action.Should().Throw<ForbiddenAccessException>();
    }

    [Fact]
    public void Administrador_deberia_gestionar_varias_sedes()
    {
        var currentUser = new CurrentUserStub(RolUsuario.Administrador, null);

        var result = ServicioRules.ValidarSedes(currentUser, [1, 2, 2]);

        result.Should().BeEquivalentTo([1, 2]);
    }

    private sealed record CurrentUserStub(RolUsuario? Rol, int? SedeId) : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
    }
}
