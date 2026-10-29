using CasaAndina.Api.Extensions;
using CasaAndina.Api.Controllers;
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

    [Fact]
    public async Task Permisos_por_accion_no_permiten_escritura_a_un_lector_aunque_su_rol_se_llame_administrador()
    {
        using var provider = CreateAuthorizationProvider();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var reader = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, "Administrador"), new Claim("permiso", "reportes.ver"),
            new Claim("permiso", "recepcion.ver")], "test"));
        (await auth.AuthorizeAsync(reader, null, "reportes.ver")).Succeeded.Should().BeTrue();
        (await auth.AuthorizeAsync(reader, null, "recepcion.ver")).Succeeded.Should().BeTrue();
        foreach (var denied in new[] { "reportes.analizar", "reportes.exportar", "recepcion.ingreso", "roles.gestionar", "usuarios.asignar" })
            (await auth.AuthorizeAsync(reader, null, denied)).Succeeded.Should().BeFalse();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permiso", "reportes.ver")]));
        (await auth.AuthorizeAsync(anonymous, null, "reportes.ver")).Succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(ReportesController), "Analizar", "reportes.analizar")]
    [InlineData(typeof(ReportesController), "Descargar", "reportes.exportar")]
    [InlineData(typeof(RecepcionController), "CheckIn", "recepcion.ingreso")]
    [InlineData(typeof(RecepcionController), "CheckOut", "recepcion.salida")]
    [InlineData(typeof(HabitacionesController), "Update", "habitaciones.editar")]
    [InlineData(typeof(ServiciosController), "Create", "servicios.crear")]
    [InlineData(typeof(UsuariosController), "Deactivate", "usuarios.desactivar")]
    [InlineData(typeof(AccesosController), "Asignar", "usuarios.asignar")]
    public void Endpoint_requiere_permiso_especifico(Type controller, string method, string permission)
    {
        controller.GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Should().Contain(a => a.Policy == permission);
    }

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

    [Fact]
    public void Chatbot_DeberiaRestringirseAlRolCliente()
    {
        var authorize = typeof(ChatbotController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        authorize.Policy.Should().Be("Cliente");
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
