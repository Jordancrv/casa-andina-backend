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
        var handler = new LoginCommandHandler(context, hasher, new TestJwtService());

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
            Id = 1,
            Contactos = { new PersonaContacto { TipoContacto = "Correo", Valor = "sin-rol@example.test" } }
        };
        context.Usuarios.Add(new Usuario
        {
            Id = 1,
            PersonaId = persona.Id,
            Persona = persona,
            PasswordHash = "test-hash"
        });
        await context.SaveChangesAsync();
        var handler = new LoginCommandHandler(context, new TestPasswordHasher(validPassword: true), new TestJwtService());

        var action = () => handler.Handle(
            new LoginCommand("sin-rol@example.test", "Password123!"), CancellationToken.None);

        var error = await action.Should().ThrowAsync<UnauthorizedAccessException>();
        error.Which.Message.Should().Be("Credenciales inválidas.");
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
}
