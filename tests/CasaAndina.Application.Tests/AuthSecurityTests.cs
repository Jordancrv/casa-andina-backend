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
            "A",
            "1234",
            "correo-invalido",
            "texto",
            "debil");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain([
            nameof(RegisterClientCommand.NombreCompleto),
            nameof(RegisterClientCommand.Dni),
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
                "  María   López ",
                "87654321",
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
        var dniType = await context.TiposDocumento.SingleAsync();
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
                "Otro Cliente",
                "87654321",
                "EXISTENTE@CORREO.COM",
                null,
                "CasaAndina2026!"),
            CancellationToken.None);

        await action.Should().ThrowAsync<ConflictException>();
        (await context.Clientes.CountAsync()).Should().Be(1);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        context.TiposDocumento.Add(new TipoDocumento
        {
            Codigo = "DNI",
            Nombre = "Documento Nacional de Identidad",
            TipoPersonaPermitida = "N",
            LongitudMinima = 8,
            LongitudMaxima = 8,
            Activo = true
        });
        context.SaveChanges();
        return context;
    }

    private sealed class FakeJwtService : IJwtService
    {
        public string GenerarToken(Usuario usuario) => "personal-token";
        public string GenerarToken(Cliente cliente) => "cliente-token";
    }
}
