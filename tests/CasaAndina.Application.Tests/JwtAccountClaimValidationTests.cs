using System.Security.Claims;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Api.Extensions;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Identity;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class JwtAccountClaimValidationTests
{
    [Fact]
    public async Task Token_se_acepta_si_los_claims_coinciden_con_la_cuenta_actual()
    {
        var accepted = await ValidateTokenAsync(
            nameof(RolUsuario.Administrador), sedeId: 4, email: "admin@example.test");

        accepted.Should().BeTrue();
    }

    [Fact]
    public async Task Token_se_rechaza_si_el_rol_fue_revocado()
    {
        var accepted = await ValidateTokenAsync(
            nameof(RolUsuario.Cliente), sedeId: 4, email: "admin@example.test");

        accepted.Should().BeFalse();
    }

    [Fact]
    public async Task Token_se_rechaza_si_la_sede_fue_cambiada()
    {
        var accepted = await ValidateTokenAsync(
            nameof(RolUsuario.Administrador), sedeId: 9, email: "admin@example.test");

        accepted.Should().BeFalse();
    }

    [Fact]
    public async Task Token_se_rechaza_si_el_correo_de_acceso_fue_cambiado()
    {
        var accepted = await ValidateTokenAsync(
            nameof(RolUsuario.Administrador), sedeId: 4, email: "old@example.test");

        accepted.Should().BeFalse();
    }

    private static async Task<bool> ValidateTokenAsync(string roleClaim, int sedeId, string email)
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(dbOptions);

        var role = new Rol
        {
            Id = 1,
            Codigo = "ADMINISTRADOR",
            Nombre = "Administrador"
        };
        var account = new Usuario
        {
            Id = 11,
            Correo = "admin@example.test",
            PasswordHash = "test-password-hash",
            SedeId = 4,
            PersonaId = 21,
            Persona = new Persona { Id = 21, TipoPersona = "N" },
            Roles =
            [
                new UsuarioRol { UsuarioId = 11, RolId = 1, Rol = role }
            ]
        };
        account.Roles.Single().Usuario = account;
        await db.Usuarios.AddAsync(account);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-secret-key-with-at-least-32-characters",
                ["Jwt:Issuer"] = "CasaAndina.Api",
                ["Jwt:Audience"] = "CasaAndina.Clients",
                ["Jwt:ExpirationHours"] = "4"
            })
            .Build();
        var authServices = new ServiceCollection();
        authServices.AddLogging();
        authServices.AddSingleton<IApplicationDbContext>(db);
        authServices.AddJwtAuthentication(configuration);
        using var authProvider = authServices.BuildServiceProvider();
        var options = authProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext { RequestServices = authProvider };
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "11"),
            new Claim(ClaimTypes.Role, roleClaim),
            new Claim(AuthClaimTypes.SedeId, sedeId.ToString()),
            new Claim(AuthClaimTypes.TipoUsuario, AuthClaimTypes.Personal),
            new Claim(ClaimTypes.Email, email)
        ], authenticationType: JwtBearerDefaults.AuthenticationScheme));
        var validationContext = new TokenValidatedContext(httpContext, scheme, options)
        {
            Principal = principal
        };
        await options.Events.OnTokenValidated(validationContext);

        return validationContext.Result is null;
    }
}
