using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class CurrentUserServiceTests
{
    [Fact]
    public void CurrentUserService_DeberiaLeerIdentidadYClienteDeLosClaims()
    {
        var service = CreateService(
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim("cliente_id", "7"),
            new Claim("sede_id", "3"),
            new Claim(ClaimTypes.Role, nameof(RolUsuario.Cliente)));

        service.UsuarioId.Should().Be(42);
        service.ClienteId.Should().Be(7);
        service.SedeId.Should().Be(3);
        service.Rol.Should().Be(RolUsuario.Cliente);
    }

    [Fact]
    public void CurrentUserService_DeberiaTratarClaimsMalformadosComoAusentes()
    {
        var service = CreateService(
            new Claim(ClaimTypes.NameIdentifier, "no-es-id"),
            new Claim("cliente_id", "0x7"),
            new Claim("sede_id", "-"),
            new Claim(ClaimTypes.Role, "RolInventado"));

        service.UsuarioId.Should().BeNull();
        service.ClienteId.Should().BeNull();
        service.SedeId.Should().BeNull();
        service.Rol.Should().BeNull();
    }

    private static CurrentUserService CreateService(params Claim[] claims)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var accessor = new HttpContextAccessor { HttpContext = context };
        return new CurrentUserService(accessor);
    }
}
