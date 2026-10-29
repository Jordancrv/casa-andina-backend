using CasaAndina.Application.Reservas.Commands;
using CasaAndina.Application.Reservas.DTOs;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common;
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
    public async Task Compra_debe_rechazar_colecciones_nulas_sin_lanzar_error()
    {
        var command = ValidCommand() with { Habitaciones = null!, Servicios = null! };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(command.Habitaciones));
        result.Errors.Should().Contain(x => x.PropertyName == nameof(command.Servicios));
    }

    [Fact]
    public async Task Compra_debe_rechazar_lista_de_huespedes_nula_sin_lanzar_error()
    {
        var command = ValidCommand() with
        {
            Habitaciones = [new HabitacionCompraDto(1, null!)]
        };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName.EndsWith("Huespedes", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Compra_debe_rechazar_elementos_nulos_sin_lanzar_error()
    {
        var command = ValidCommand() with
        {
            Habitaciones = [null!],
            Servicios = [null!]
        };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Compra_debe_rechazar_huesped_nulo_sin_lanzar_error()
    {
        var command = ValidCommand() with
        {
            Habitaciones = [new HabitacionCompraDto(1, [null!])]
        };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
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

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("Desconocido")]
    public async Task Tipo_comprobante_solo_acepta_los_codigos_textuales_permitidos(string tipo)
    {
        var command = ValidCommand() with { TipoComprobante = tipo };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(command.TipoComprobante));
    }

    [Fact]
    public async Task Compra_debe_rechazar_horas_en_las_fechas_de_estadia()
    {
        var command = ValidCommand() with { FechaCheckIn = PeruBusinessTime.Today.AddDays(1).AddHours(1) };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorMessage.Contains("sin hora", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("telefono-invalido")]
    [InlineData("12345")]
    [InlineData("+0123456789")]
    public async Task Compra_debe_rechazar_telefono_de_huesped_invalido(string telefono)
    {
        var command = ValidCommand() with
        {
            Habitaciones = [new HabitacionCompraDto(1, [Guest(true) with { Telefono = telefono }])]
        };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName.Contains("Telefono", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("987654321")]
    [InlineData("+51987654321")]
    public async Task Compra_debe_aceptar_telefono_nacional_o_internacional(string telefono)
    {
        var command = ValidCommand() with
        {
            Habitaciones = [new HabitacionCompraDto(1, [Guest(true) with { Telefono = telefono }])]
        };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Jordan2", "Carhuallay")]
    [InlineData("Jordan", "Carhuallay!")]
    [InlineData("<script>", "Carhuallay")]
    public async Task Compra_debe_rechazar_nombres_con_numeros_o_simbolos(string nombres, string apellidos)
    {
        var command = ValidCommand() with
        {
            Habitaciones = [new HabitacionCompraDto(1, [Guest(true) with { Nombres = nombres, Apellidos = apellidos }])]
        };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("María José", "De la Cruz")]
    [InlineData("O’Neil", "Jean-Luc")]
    [InlineData("D'Angelo", "Muñoz")]
    public async Task Compra_debe_aceptar_nombres_con_acentos_y_separadores_validos(string nombres, string apellidos)
    {
        var command = ValidCommand() with
        {
            Habitaciones = [new HabitacionCompraDto(1, [Guest(true) with { Nombres = nombres, Apellidos = apellidos }])]
        };

        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Inicio_del_dia_operativo_corresponde_a_medianoche_de_Peru_en_UTC()
    {
        var utc = PeruBusinessTime.UtcStartOfDay(new DateTime(2026, 10, 17));

        utc.Should().Be(new DateTime(2026, 10, 17, 5, 0, 0, DateTimeKind.Utc));
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

    [Fact]
    public async Task Huesped_requiere_dni_fecha_de_nacimiento_y_sexo_validos()
    {
        var invalid = Guest(true) with { Dni = "123", FechaNacimiento = PeruBusinessTime.Today.AddDays(1), Sexo = "X" };
        var command = ValidCommand() with { Habitaciones = [new HabitacionCompraDto(1, [invalid])] };
        var result = await new CrearReservaCompraCommandValidator().ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.EndsWith("Dni"));
        result.Errors.Should().Contain(e => e.PropertyName.EndsWith("FechaNacimiento"));
        result.Errors.Should().Contain(e => e.PropertyName.EndsWith("Sexo"));
    }

    private static CrearReservaCompraCommand ValidCommand() => new(
        1, PeruBusinessTime.Today.AddDays(1), PeruBusinessTime.Today.AddDays(2),
        [new HabitacionCompraDto(1, [Guest(true)])], [], "Boleta", null, null, "YAPE");

    private static HuespedCompraDto Guest(bool titular) =>
        new("Jordan", "Prueba", "jordan@example.test", null, titular, "12345678", new DateTime(1990, 1, 1), "M");
}
