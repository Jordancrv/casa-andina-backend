using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class AuthSecurityTests
{
    [Theory]
    [InlineData("ADMINISTRADOR", RolUsuario.Administrador)]
    [InlineData("RECEPCION", RolUsuario.Recepcion)]
    [InlineData("OPERACIONES", RolUsuario.Operaciones)]
    [InlineData("MANTENIMIENTO", RolUsuario.Mantenimiento)]
    public void Rol_DeberiaUsarCodigoEstable(string codigo, RolUsuario esperado)
    {
        var usuario = new Usuario
        {
            RolId = 99,
            RolAsignado = new Rol { Id = 99, Codigo = codigo, Nombre = "Nombre visible" }
        };

        usuario.Rol.Should().Be(esperado);
    }

    [Fact]
    public void PasswordHasher_DeberiaAceptarHashBCrypt()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("ClaveSegura123");

        hasher.Verify("ClaveSegura123", hash).Should().BeTrue();
        hasher.Verify("OtraClave", hash).Should().BeFalse();
    }

    [Fact]
    public void PasswordHasher_DeberiaRechazarTextoPlano()
    {
        var hasher = new PasswordHasher();

        hasher.Verify("jordan", "jordan").Should().BeFalse();
    }
}
