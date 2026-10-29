using CasaAndina.Application.Comodidades;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ComodidadesTests
{
    private sealed record User(RolUsuario? Rol, int? SedeId) : ICurrentUserService
    {
        public int? UsuarioId => 1;
        public int? ClienteId => null;
    }

    [Fact]
    public async Task Crear_normaliza_nombre_y_rechaza_duplicados()
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var handler = new CrearComodidadHandler(context, new User(RolUsuario.Administrador, 1));
        var id = await handler.Handle(new("  WiFi  "), default);
        (await context.Comodidades.FindAsync(id))!.Nombre.Should().Be("WiFi");
        await FluentActions.Awaiting(() => handler.Handle(new("wifi"), default)).Should().ThrowAsync<ConflictException>();
        await FluentActions.Awaiting(() => new CrearComodidadHandler(context, new User(RolUsuario.Administrador, null))
            .Handle(new("TV"), default)).Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Nombre_vacio_no_es_valido(string nombre)
        => (await new CrearComodidadValidator().ValidateAsync(new CrearComodidadCommand(nombre))).IsValid.Should().BeFalse();
}
