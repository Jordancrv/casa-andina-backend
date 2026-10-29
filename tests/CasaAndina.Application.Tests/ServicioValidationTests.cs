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
    public async Task Servicio_compartido_oculta_otras_sedes_y_no_se_puede_editar_localmente()
    {
        await using var context = CreateContext();
        var servicio = new CasaAndina.Domain.Entities.Servicio { Id = 1, Nombre = "Desayuno", Precio = 20, Activo = true };
        foreach (var id in new[] { 1, 2 }) servicio.Sedes.Add(new CasaAndina.Domain.Entities.Sede {
            Id = id, Nombre = $"Sede {id}", Codigo = $"S{id}", Ciudad = "Lima", Region = "Costa", Categoria = "Select", Activo = true });
        context.Servicios.Add(servicio);
        await context.SaveChangesAsync();
        var local = new CurrentUserStub(RolUsuario.Administrador, 1);
        var detail = await new GetServicioByIdQueryHandler(context, local).Handle(new(1), default);
        detail.SedesIds.Should().Equal(1);
        detail.Sedes.Should().Equal("Sede 1");
        detail.PuedeEditar.Should().BeFalse();
        var list = await new GetServiciosQueryHandler(context, local).Handle(new(null, null), default);
        list.Items.Single().SedesIds.Should().Equal(1);
        list.Items.Single().PuedeEditar.Should().BeFalse();
        var global = await new GetServicioByIdQueryHandler(context, new CurrentUserStub(RolUsuario.Superadministrador, null)).Handle(new(1), default);
        global.SedesIds.Should().HaveCount(2);
        global.PuedeEditar.Should().BeTrue();
        var denied = () => new UpdateServicioCommandHandler(context, local).Handle(new() { Id = 1, Nombre = "Modificado", Precio = 1, SedesIds = [1] }, default);
        await denied.Should().ThrowAsync<ForbiddenAccessException>();
        servicio.Nombre.Should().Be("Desayuno");
    }

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
    public async Task Crear_y_actualizar_deberian_rechazar_nombre_de_servicio_en_blanco()
    {
        var create = await new CreateServicioCommandValidator().ValidateAsync(
            new CreateServicioCommand("   ", null, 10m, [1]));
        var update = await new UpdateServicioCommandValidator().ValidateAsync(new UpdateServicioCommand
        {
            Id = 1, Nombre = "  ", Precio = 10m, SedesIds = [1]
        });

        create.Errors.Should().Contain(x => x.PropertyName == nameof(CreateServicioCommand.Nombre));
        update.Errors.Should().Contain(x => x.PropertyName == nameof(UpdateServicioCommand.Nombre));
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
