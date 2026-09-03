using CasaAndina.Application.Auth.Commands;
using FluentAssertions;
using Xunit;

namespace CasaAndina.Application.Tests;

/// <summary>
/// Ejemplo mínimo: los validadores de FluentValidation son Application puro,
/// se testean sin levantar la API ni SQL Server (útil para la Semana 13 - QA).
/// </summary>
public class LoginCommandValidatorTests
{
    [Theory]
    [InlineData("", "123456", false)]
    [InlineData("correo-invalido", "123456", false)]
    [InlineData("user@casaandina.com", "123", false)]
    [InlineData("user@casaandina.com", "123456", true)]
    public void Validate_DeberiaRespetarReglas(string email, string password, bool esValido)
    {
        var validator = new LoginCommandValidator();
        var result = validator.Validate(new LoginCommand(email, password));

        result.IsValid.Should().Be(esValido);
    }
}
