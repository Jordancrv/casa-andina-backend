using CasaAndina.Application.Auth.Commands;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Api.Extensions;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class AuthSecurityTests
{
    private const string TestJwtKey = "test-secret-key-with-at-least-32-characters";

    [Theory]
    [InlineData("1", 1)]
    [InlineData("4", 4)]
    [InlineData("8", 8)]
    public void Jwt_DeberiaAceptarExpiracionesDentroDelRango(string configured, double expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:ExpirationHours"] = configured
            })
            .Build();

        JwtTokenSettings.GetExpirationHours(configuration).Should().Be(expected);
    }

    [Fact]
    public void Jwt_DeberiaUsarCuatroHorasCuandoNoHayConfiguracion()
    {
        var configuration = new ConfigurationBuilder().Build();

        JwtTokenSettings.GetExpirationHours(configuration).Should().Be(4);
    }

    [Theory]
    [InlineData("Jwt:Issuer")]
    [InlineData("Jwt:Audience")]
    public void Jwt_DeberiaRechazarIssuerOAudienceVacios(string missingSetting)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "CasaAndina.Api",
            ["Jwt:Audience"] = "CasaAndina.Clients",
            ["Jwt:ExpirationHours"] = "4"
        };
        settings.Remove(missingSetting);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var act = () => JwtTokenSettings.ValidateAndGetExpirationHours(configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Jwt_DeberiaEmitirTokenConExpiracionYClaimsEsperados()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "CasaAndina.Api",
                ["Jwt:Audience"] = "CasaAndina.Clients",
                ["Jwt:Key"] = "test-secret-key-with-at-least-32-characters",
                ["Jwt:ExpirationHours"] = "4"
            })
            .Build();
        var user = new Usuario
        {
            Id = 42,
            Persona = new Persona
            {
                Natural = new PersonaNatural { Nombres = "Cliente", Apellidos = "Prueba" },
                Cliente = new Cliente { Id = 7 },
                Contactos =
                {
                    new PersonaContacto
                    {
                        TipoContacto = "Correo",
                        Valor = "cliente@example.test",
                        EsPrincipal = true
                    }
                }
            },
            Roles =
            {
                new UsuarioRol { Rol = new Rol { Codigo = "CLIENTE" } }
            }
        };
        var expectedExpiration = DateTime.UtcNow.AddHours(4);

        var rawToken = new JwtService(configuration).GenerarToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(rawToken);

        token.Issuer.Should().Be("CasaAndina.Api");
        token.Audiences.Should().Contain("CasaAndina.Clients");
        token.Claims.Should().Contain(claim => claim.Type == System.Security.Claims.ClaimTypes.Role
            && claim.Value == nameof(RolUsuario.Cliente));
        Math.Abs((token.ValidTo - expectedExpiration).TotalSeconds).Should().BeLessThan(2);
    }

    [Fact]
    public void Jwt_ConfiguracionApi_DeberiaAceptarTokenValido()
    {
        var configuration = CreateJwtConfiguration();
        var validationParameters = CreateJwtValidationParameters(configuration);
        var validToken = CreateSignedToken("CasaAndina.Api", "CasaAndina.Clients", TestJwtKey,
            DateTime.UtcNow.AddMinutes(5));

        var principal = new JwtSecurityTokenHandler()
            .ValidateToken(validToken, validationParameters, out _);

        principal.FindFirst(ClaimTypes.Role)?.Value.Should().Be(nameof(RolUsuario.Cliente));
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("expiration")]
    public void Jwt_ConfiguracionApi_DeberiaRechazarTokenInvalido(string defect)
    {
        var validationParameters = CreateJwtValidationParameters(CreateJwtConfiguration());
        var issuer = defect == "issuer" ? "Otro.Emisor" : "CasaAndina.Api";
        var audience = defect == "audience" ? "Otro.Audience" : "CasaAndina.Clients";
        var key = defect == "signature" ? "another-invalid-signing-key-with-32-chars" : TestJwtKey;
        var expiration = defect == "expiration"
            ? DateTime.UtcNow.AddMinutes(-2)
            : DateTime.UtcNow.AddMinutes(5);
        var invalidToken = CreateSignedToken(issuer, audience, key, expiration);
        var act = () => new JwtSecurityTokenHandler()
            .ValidateToken(invalidToken, validationParameters, out _);

        act.Should().Throw<SecurityTokenException>();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("8.1")]
    [InlineData("NaN")]
    [InlineData("infinity")]
    public void Jwt_DeberiaRechazarExpiracionesInvalidas(string configured)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:ExpirationHours"] = configured
            })
            .Build();

        var act = () => JwtTokenSettings.GetExpirationHours(configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("ADMINISTRADOR", RolUsuario.Administrador)]
    [InlineData("RECEPCION", RolUsuario.Recepcion)]
    [InlineData("OPERACIONES", RolUsuario.Operaciones)]
    [InlineData("MANTENIMIENTO", RolUsuario.Mantenimiento)]
    public void Rol_DeberiaUsarCodigoEstable(string codigo, RolUsuario esperado)
    {
        var usuario = new Usuario
        {
            Roles =
            {
                new UsuarioRol { Rol = new Rol { Id = 99, Codigo = codigo, Nombre = "Nombre visible" } }
            }
        };

        RolCodigo.Mapear(usuario.Roles.Single().Rol.Codigo).Should().Be(esperado);
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

    [Fact]
    public void Registro_DeberiaRechazarDatosDebilesOMalformados()
    {
        var validator = new RegisterClientCommandValidator();
        var command = new RegisterClientCommand(
            "X",
            "DNI",
            "1234",
            "A",
            null,
            "correo-invalido",
            "texto",
            "debil");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain([
            nameof(RegisterClientCommand.NombreCompleto),
            nameof(RegisterClientCommand.TipoPersona),
            nameof(RegisterClientCommand.NumeroDocumento),
            nameof(RegisterClientCommand.Email),
            nameof(RegisterClientCommand.Telefono),
            nameof(RegisterClientCommand.Password)
        ]);
    }

    [Fact]
    public void Login_DeberiaLimitarLongitudDeContrasena()
    {
        var validator = new LoginCommandValidator();
        var command = new LoginCommand("cliente@correo.com", new string('A', 73));

        validator.Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Registro_DeberiaCrearClienteConHashYRolCliente()
    {
        await using var context = CreateContext();
        var handler = new RegisterClientCommandHandler(context, new PasswordHasher(), new FakeJwtService());
        var password = "CasaAndina2026!";

        var response = await handler.Handle(
            new RegisterClientCommand(
                "N",
                "DNI",
                "87654321",
                "  María   López ",
                null,
                "MARIA@CORREO.COM ",
                "+51987654321",
                password),
            CancellationToken.None);

        var cliente = await context.Clientes.SingleAsync();
        var usuario = await context.Usuarios.Include(x => x.Roles).ThenInclude(x => x.Rol).SingleAsync();
        response.Rol.Should().Be("Cliente");
        cliente.Estado.Should().Be("Activo");
        usuario.PasswordHash.Should().NotBe(password);
        usuario.Correo.Should().Be("maria@correo.com");
        new PasswordHasher().Verify(password, usuario.PasswordHash).Should().BeTrue();
        usuario.Roles.Single().Rol.Codigo.Should().Be("CLIENTE");
        (await context.PersonasNaturales.CountAsync()).Should().Be(1);
        (await context.PersonaDocumentos.SingleAsync()).Numero.Should().Be("87654321");
        (await context.PersonaContactos.CountAsync(x => x.TipoContacto == "Correo")).Should().Be(0);
    }

    [Fact]
    public async Task Registro_DeberiaRechazarCorreoODniDuplicados()
    {
        await using var context = CreateContext();
        var dniType = await context.TiposDocumento.SingleAsync(x => x.Codigo == "DNI");
        var persona = new Persona
        {
            TipoPersona = "N",
            Natural = new PersonaNatural { Nombres = "Cliente Existente" }
        };
        persona.Documentos.Add(new PersonaDocumento
        {
            TipoDocumentoId = dniType.Id,
            Numero = "12345678",
            EsPrincipal = true
        });
        persona.Contactos.Add(new PersonaContacto
        {
            TipoContacto = "Correo",
            Valor = "existente@correo.com",
            EsPrincipal = true
        });
        persona.Cliente = new Cliente
        {
            Persona = persona,
            Estado = "Activo"
        };
        context.Personas.Add(persona);
        context.Usuarios.Add(new Usuario
        {
            Correo = "existente@correo.com",
            Persona = persona,
            PasswordHash = "hash-de-prueba"
        });
        await context.SaveChangesAsync();
        var handler = new RegisterClientCommandHandler(context, new PasswordHasher(), new FakeJwtService());

        var action = () => handler.Handle(
            new RegisterClientCommand(
                "N",
                "DNI",
                "87654321",
                "Otro Cliente",
                null,
                "EXISTENTE@CORREO.COM",
                null,
                "CasaAndina2026!"),
            CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>();
        (await context.Clientes.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Registro_DeberiaCrearPersonaJuridicaConRuc()
    {
        await using var context = CreateContext();
        var handler = new RegisterClientCommandHandler(context, new PasswordHasher(), new FakeJwtService());

        var response = await handler.Handle(
            new RegisterClientCommand(
                "J",
                "RUC",
                "20123456789",
                "Servicios Hoteleros Andinos SAC",
                "Andinos",
                "empresa@correo.com",
                null,
                "CasaAndina2026!"),
            CancellationToken.None);

        response.Rol.Should().Be("Cliente");
        (await context.PersonasJuridicas.SingleAsync()).RazonSocial
            .Should().Be("Servicios Hoteleros Andinos SAC");
        (await context.PersonaDocumentos.SingleAsync()).Numero.Should().Be("20123456789");
        (await context.PersonasNaturales.CountAsync()).Should().Be(0);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        context.TiposDocumento.AddRange(
            new TipoDocumento { Codigo = "DNI", Nombre = "Documento Nacional de Identidad", TipoPersonaPermitida = "N", LongitudMinima = 8, LongitudMaxima = 8, Activo = true },
            new TipoDocumento { Codigo = "CE", Nombre = "Carné de Extranjería", TipoPersonaPermitida = "N", LongitudMinima = 9, LongitudMaxima = 12, Activo = true },
            new TipoDocumento { Codigo = "PASAPORTE", Nombre = "Pasaporte", TipoPersonaPermitida = "N", LongitudMinima = 6, LongitudMaxima = 20, Activo = true },
            new TipoDocumento { Codigo = "RUC", Nombre = "Registro Único de Contribuyentes", TipoPersonaPermitida = "J", LongitudMinima = 11, LongitudMaxima = 11, Activo = true });
        context.Roles.Add(new Rol { Codigo = "CLIENTE", Nombre = "Cliente" });
        context.SaveChanges();
        return context;
    }

    private static IConfiguration CreateJwtConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "CasaAndina.Api",
            ["Jwt:Audience"] = "CasaAndina.Clients",
            ["Jwt:Key"] = TestJwtKey,
            ["Jwt:ExpirationHours"] = "4"
        })
        .Build();

    private static TokenValidationParameters CreateJwtValidationParameters(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJwtAuthentication(configuration);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
    }

    private static string CreateSignedToken(string issuer, string audience, string signingKey, DateTime expiration)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: [new Claim(ClaimTypes.Role, nameof(RolUsuario.Cliente))],
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: expiration,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class FakeJwtService : IJwtService
    {
        public string GenerarToken(Usuario usuario) => "usuario-token";
    }
}
