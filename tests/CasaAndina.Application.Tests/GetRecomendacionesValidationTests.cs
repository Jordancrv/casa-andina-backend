using CasaAndina.Application.Chatbot.Queries;
using CasaAndina.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class GetRecomendacionesValidationTests
{
    private readonly GetRecomendacionesQueryValidator _validator = new();

    [Theory]
    [InlineData(NivelPresupuesto.Economico, TipoClima.Calido)]
    [InlineData(NivelPresupuesto.Estandar, TipoClima.Frio)]
    [InlineData(NivelPresupuesto.Lujo, TipoClima.Templado)]
    public void Recomendaciones_DeberiaAceptarValoresDefinidos(NivelPresupuesto presupuesto, TipoClima clima)
    {
        var result = _validator.Validate(new GetRecomendacionesQuery(presupuesto, clima));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(4, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 4)]
    public void Recomendaciones_DeberiaRechazarValoresFueraDelCatalogo(int presupuesto, int clima)
    {
        var result = _validator.Validate(new GetRecomendacionesQuery(
            (NivelPresupuesto)presupuesto,
            (TipoClima)clima));

        result.IsValid.Should().BeFalse();
    }
}
