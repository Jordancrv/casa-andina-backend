using CasaAndina.Application.Reservas.Commands;
using CasaAndina.Application.Reservas.DTOs;
using CasaAndina.Application.Common.Exceptions;
using FluentAssertions;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ReservaCompraValidationTests
{
    [Fact]
    public async Task Compra_debe_requerir_al_menos_una_habitacion()
    {
        var command = ValidCommand() with { Habitaciones = [] };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(command.Habitaciones));
    }

    [Fact]
    public async Task Cada_habitacion_debe_identificar_un_huesped_principal()
    {
        var command = ValidCommand() with
        {
            Habitaciones = [new HabitacionCompraDto(1, [Guest(false)])]
        };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Compra_debe_limitar_la_cantidad_de_habitaciones()
    {
        var rooms = Enumerable.Range(1, 9)
            .Select(id => new HabitacionCompraDto(id, [Guest(true)]))
            .ToArray();
        var command = ValidCommand() with { Habitaciones = rooms };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Factura_debe_exigir_razon_social_y_ruc()
    {
        var command = ValidCommand() with { TipoComprobante = "Factura" };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(command.RazonSocial));
        result.Errors.Should().Contain(x => x.PropertyName == nameof(command.Ruc));
    }

    [Fact]
    public async Task Boleta_no_debe_aceptar_datos_de_factura()
    {
        var command = ValidCommand() with { RazonSocial = "Empresa", Ruc = "20123456789" };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Importe_maximo_decimal_10_2_es_aceptado()
    {
        var importe = () => ReservaCompraRules.ValidarImportePersistible(
            ReservaCompraRules.MaximoImportePersistible, "Total de reserva");

        importe.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100000000)]
    public void Importe_fuera_de_decimal_10_2_se_rechaza(decimal valor)
    {
        var importe = () => ReservaCompraRules.ValidarImportePersistible(valor, "Total de reserva");

        importe.Should().Throw<ValidationException>();
    }

    private static CrearReservaCompraCommand ValidCommand() => new(
        1, DateTime.Today.AddDays(1), DateTime.Today.AddDays(2),
        [new HabitacionCompraDto(1, [Guest(true)])], [], "Boleta", null, null);

    private static HuespedCompraDto Guest(bool titular) =>
        new("Jordan", "Prueba", "jordan@example.test", null, titular);
}
