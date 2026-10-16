using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Servicios;
using CasaAndina.Application.Servicios.Commands;
using CasaAndina.Application.Servicios.Queries;
using CasaAndina.Infrastructure.Persistence;
using CasaAndina.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
    public void Administrador_deberia_limitar_servicios_a_su_sede()
    {
        var currentUser = new CurrentUserStub(RolUsuario.Administrador, 1);

        var result = ServicioRules.ValidarSedes(currentUser, [1, 1]);

        result.Should().BeEquivalentTo([1]);
        var action = () => ServicioRules.ValidarSedes(currentUser, [2]);
        action.Should().Throw<ForbiddenAccessException>();
    }

    [Fact]
    public void Administrador_sin_sede_no_puede_gestionar_servicios()
    {
        var currentUser = new CurrentUserStub(RolUsuario.Administrador, null);

        var action = () => ServicioRules.ValidarSedes(currentUser, [1]);

        action.Should().Throw<ForbiddenAccessException>();
    }

    [Fact]
    public void Superadministrador_puede_gestionar_varias_sedes()
    {
        var currentUser = new CurrentUserStub(RolUsuario.Superadministrador, null);

        var result = ServicioRules.ValidarSedes(currentUser, [1, 2, 2]);

        result.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task Listado_rechaza_administrador_sin_sede()
    {
        await using var context = CreateContext();
        var handler = new GetServiciosQueryHandler(
            context, new CurrentUserStub(RolUsuario.Administrador, SedeId: null));

        var action = () => handler.Handle(new GetServiciosQuery(null, null), CancellationToken.None);

        await action.Should().ThrowAsync<ForbiddenAccessException>();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }

    private sealed record CurrentUserStub(RolUsuario? Rol, int? SedeId) : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
    }
}
