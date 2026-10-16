using CasaAndina.Application.Auth.Commands;
using CasaAndina.Application.Auth.DTOs;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class LoginSecurityTests
{
    [Fact]
    public async Task Login_DeberiaEjecutarVerificacionBCryptFicticiaSiNoExisteElCorreo()
    {
        await using var context = CreateContext();
        var hasher = new TestPasswordHasher();
        var handler = new LoginCommandHandler(context, hasher, new TestJwtService(), new TestRecaptchaVerifier());

        var action = () => handler.Handle(
            new LoginCommand("inexistente@example.test", "Password123!"), CancellationToken.None);

        var error = await action.Should().ThrowAsync<UnauthorizedAccessException>();
        error.Which.Message.Should().Be("Credenciales inválidas.");
        hasher.DummyVerificationCount.Should().Be(1);
    }

    [Fact]
    public async Task Login_DeberiaUsarMensajeGenericoSiCuentaNoTieneRol()
    {
        await using var context = CreateContext();
        var persona = new Persona
        {
            Id = 1
        };
        context.Usuarios.Add(new Usuario
        {
            Id = 1,
            Correo = "sin-rol@example.test",
            PersonaId = persona.Id,
            Persona = persona,
            PasswordHash = "test-hash"
        });
        await context.SaveChangesAsync();
        var handler = new LoginCommandHandler(context, new TestPasswordHasher(validPassword: true), new TestJwtService(), new TestRecaptchaVerifier());

        var action = () => handler.Handle(
            new LoginCommand("sin-rol@example.test", "Password123!"), CancellationToken.None);

        var error = await action.Should().ThrowAsync<UnauthorizedAccessException>();
        error.Which.Message.Should().Be("Credenciales inválidas.");
    }

    [Fact]
    public async Task Login_UsaSoloCorreoDeUsuarioYNoLosContactosDeNotificacion()
    {
        await using var context = CreateContext();
        var persona = new Persona
        {
            Id = 1,
            Natural = new PersonaNatural { Nombres = "Ana" },
            Contactos =
            {
                new PersonaContacto { TipoContacto = "Correo", Valor = "notificaciones@example.test", EsPrincipal = true }
            }
        };
        var rol = new Rol { Id = 1, Codigo = "CLIENTE", Nombre = "Cliente" };
        context.Usuarios.Add(new Usuario
        {
            Id = 1,
            Correo = "acceso@example.test",
            PersonaId = 1,
            Persona = persona,
            PasswordHash = "test-hash",
            Roles = { new UsuarioRol { Rol = rol } }
        });
        await context.SaveChangesAsync();
        var hasher = new TestPasswordHasher(validPassword: true);
        var handler = new LoginCommandHandler(context, hasher, new TestJwtService(), new TestRecaptchaVerifier());

        var wrongIdentifier = () => handler.Handle(
            new LoginCommand("notificaciones@example.test", "Password123!"), CancellationToken.None);
        await wrongIdentifier.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Credenciales inválidas.");
        hasher.DummyVerificationCount.Should().Be(1);

        var response = await handler.Handle(
            new LoginCommand(" ACCESO@example.test ", "Password123!"), CancellationToken.None);
        response.Token.Should().Be("test-token");
        hasher.DummyVerificationCount.Should().Be(1);
    }

    [Fact]
    public async Task Login_RechazaCaptchaInvalidoAntesDeVerificarCredenciales()
    {
        await using var context = CreateContext();
        var hasher = new TestPasswordHasher();
        var handler = new LoginCommandHandler(context, hasher, new TestJwtService(), new TestRecaptchaVerifier(valid: false));

        var action = () => handler.Handle(
            new LoginCommand("cliente@example.test", "Password123!", "token-invalido"), CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Credenciales inválidas.");
        hasher.DummyVerificationCount.Should().Be(0);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class TestPasswordHasher(bool validPassword = false) : IPasswordHasher
    {
        public int DummyVerificationCount { get; private set; }
        public string Hash(string password) => "test-hash";
        public bool Verify(string password, string hash) => validPassword;
        public void VerifyAgainstDummyHash(string password) => DummyVerificationCount++;
    }

    private sealed class TestJwtService : IJwtService
    {
        public string GenerarToken(Usuario usuario) => "test-token";
    }

    private sealed class TestRecaptchaVerifier(bool valid = true) : IRecaptchaVerifier
    {
        public Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken)
            => Task.FromResult(valid);
    }
}
