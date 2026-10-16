using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Reservas.Queries;
using FluentAssertions;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class CalendarioReservasValidationTests
{
    [Fact]
    public void Rango_de_calendario_acepta_periodo_valido_incluso_historico()
    {
        var desde = new DateTime(2025, 1, 1);

        var validar = () => GetCalendarioReservasQueryHandler.ValidarRango(1, desde, desde.AddDays(30));

        validar.Should().NotThrow();
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 0)]
    [InlineData(1, 367)]
    public void Rango_de_calendario_rechaza_sede_o_fechas_invalidas(int sedeId, int dias)
    {
        var desde = new DateTime(2025, 1, 1);

        var validar = () => GetCalendarioReservasQueryHandler.ValidarRango(sedeId, desde, desde.AddDays(dias));

        validar.Should().Throw<ValidationException>();
    }
}
