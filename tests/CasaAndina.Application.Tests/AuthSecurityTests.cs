using CasaAndina.Application.Auth.Commands;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        response.Rol.Should().Be("Cliente");
        cliente.Correo.Should().Be("maria@correo.com");
        cliente.NombreCompleto.Should().Be("María López");
        cliente.Estado.Should().Be("Activo");
        cliente.ContrasenaHash.Should().NotBe(password);
        new PasswordHasher().Verify(password, cliente.ContrasenaHash!).Should().BeTrue();
        (await context.PersonasNaturales.CountAsync()).Should().Be(1);
        (await context.PersonaDocumentos.SingleAsync()).Numero.Should().Be("87654321");
        (await context.PersonaCorreos.SingleAsync()).Correo.Should().Be("maria@correo.com");
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
        persona.Correos.Add(new PersonaCorreo
        {
            Correo = "existente@correo.com",
            EsPrincipal = true
        });
        persona.Cliente = new Cliente
        {
            Persona = persona,
            NombreCompleto = "Cliente Existente",
            DNI = "12345678",
            Correo = "existente@correo.com",
            Estado = "Activo"
        };
        context.Personas.Add(persona);
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
        context.SaveChanges();
        return context;
    }

    private sealed class FakeJwtService : IJwtService
    {
        public string GenerarToken(Usuario usuario) => "personal-token";
        public string GenerarToken(Cliente cliente) => "cliente-token";
    }
}
