using CasaAndina.Application.Dashboard.Queries;
using FluentAssertions;
using Xunit;

namespace CasaAndina.Application.Tests;

public class ProyeccionOcupacionTests
{
    private static readonly DateTime Hoy = new(2026, 10, 28); // miércoles

    [Fact]
    public void Sin_historial_muestra_reservas_reales_sin_inventar_estimacion()
    {
        var rows = new[] { new OcupacionRegistro(1, new(2026, 11, 1), new(2026, 11, 4)) };
        var result = ProyeccionOcupacion.Calcular(Hoy, 2, null, rows);
        result.Disponible.Should().BeFalse();
        result.Semanas[0].Inicio.Should().Be(new DateTime(2026, 11, 2));
        result.Semanas[0].ReservadasNoches.Should().Be(2);
        result.Semanas[0].EstimadasNoches.Should().BeNull();
    }

    [Fact]
    public void Cuenta_habitaciones_por_noche_sin_duplicar_y_respeta_capacidad()
    {
        var rows = Enumerable.Range(0, 10).Select(i => new OcupacionRegistro(1, new DateTime(2026, 9, 1).AddDays(i * 4), new DateTime(2026, 9, 3).AddDays(i * 4))).ToList();
        rows.Add(new(1, new(2026, 11, 2), new(2026, 11, 9)));
        rows.Add(new(1, new(2026, 11, 2), new(2026, 11, 9)));
        var result = ProyeccionOcupacion.Calcular(Hoy, 2, new DateTime(2026, 7, 1), rows);
        result.Disponible.Should().BeTrue();
        result.Semanas[0].ReservadasNoches.Should().Be(7);
        result.Semanas[0].EstimadasNoches.Should().BeGreaterThanOrEqualTo(7).And.BeLessThanOrEqualTo(14);
        result.Semanas[0].ReservadasPct.Should().Be(50);
    }

    [Fact]
    public void Semana_parcial_y_sede_sin_habitaciones_no_producen_estimacion()
    {
        var result = ProyeccionOcupacion.Calcular(Hoy, 0, new DateTime(2026, 1, 1), []);
        result.Disponible.Should().BeFalse();
        result.Semanas.Should().OnlyContain(w => w.EstimadasNoches == null && w.ReservadasPct == 0);
    }
}
