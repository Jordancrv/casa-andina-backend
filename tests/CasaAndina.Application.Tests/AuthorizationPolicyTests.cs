using CasaAndina.Api.Extensions;
using CasaAndina.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class AuthorizationPolicyTests
{
    private const string TestJwtKey = "test-secret-key-with-at-least-32-characters";

    [Theory]
    [InlineData(nameof(RolUsuario.Cliente), "Cliente", true)]
    [InlineData(nameof(RolUsuario.Cliente), "PersonalInterno", false)]
    [InlineData(nameof(RolUsuario.Recepcion), "PersonalInterno", true)]
    [InlineData(nameof(RolUsuario.Recepcion), "GestionHabitaciones", false)]
    [InlineData(nameof(RolUsuario.Operaciones), "GestionHabitaciones", true)]
    [InlineData(nameof(RolUsuario.Mantenimiento), "GestionServicios", false)]
    [InlineData(nameof(RolUsuario.Administrador), "GestionServicios", true)]
    [InlineData(nameof(RolUsuario.Superadministrador), "Administrador", false)]
    public async Task Politicas_DeberianAutorizarSoloRolesConfigurados(
        string role, string policy, bool expected)
    {
        using var provider = CreateAuthorizationProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "test-user"), new Claim(ClaimTypes.Role, role)],
            authenticationType: "test"));

        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, resource: null, policy);

        result.Succeeded.Should().Be(expected);
    }

    [Fact]
    public async Task Politica_DeberiaDenegarUsuarioSinAutenticar()
    {
        using var provider = CreateAuthorizationProvider();

        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), resource: null, "Cliente");

        result.Succeeded.Should().BeFalse();
    }

    private static ServiceProvider CreateAuthorizationProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "CasaAndina.Api",
                ["Jwt:Audience"] = "CasaAndina.Clients",
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:ExpirationHours"] = "4"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJwtAuthentication(configuration);
        return services.BuildServiceProvider();
    }
}
