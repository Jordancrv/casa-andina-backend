using CasaAndina.Application.Chatbot.DTOs;
using CasaAndina.Application.Chatbot.Queries;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Infrastructure.Services;

/// <summary>El catálogo decide qué es reservable; Gemini ordena y explica opciones verificadas.</summary>
public sealed class GeminiRecommendationService(ApplicationDbContext context,
    ITravelExplanationClient assistant, ITravelWeatherService weather) : IRecommendationService
{
    public async Task<IReadOnlyList<DestinoRecomendadoDto>> RecomendarDestinosAsync(
        NivelPresupuesto presupuesto, TipoClima clima, CancellationToken cancellationToken = default)
    {
        // Compatibilidad con el GET original: bandas orientativas, sin validar fechas.
        var limite = presupuesto switch { NivelPresupuesto.Economico => 250m, NivelPresupuesto.Estandar => 500m, _ => 10000m };
        var region = clima switch { TipoClima.Frio => "Sierra", TipoClima.Templado => "Costa", _ => "Selva" };
        return await context.Sedes.AsNoTracking()
            .Where(s => s.Activo && s.Habitaciones.Any(h => h.Activo && h.PrecioNoche > 0 && h.PrecioNoche <= limite && (h.Estado == "Disponible" || h.Estado == "Ocupada")))
            .OrderByDescending(s => s.Region == region).ThenBy(s => s.Id).Take(3)
            .Select(s => new DestinoRecomendadoDto(s.Id, s.Nombre, s.Ciudad, s.Region, s.Categoria,
                s.Habitaciones.Where(h => h.Activo && h.PrecioNoche > 0 && h.PrecioNoche <= limite && (h.Estado == "Disponible" || h.Estado == "Ocupada")).Min(h => h.PrecioNoche),
                "Precio referencial. Consulta el planificador con fechas para verificar disponibilidad y clima."))
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanViajeDto> PlanificarAsync(PlanificarViajeQuery consulta, CancellationToken cancellationToken)
    {
        var ciudad = consulta.Ciudad?.Trim();
        var habitaciones = await context.Habitaciones.AsNoTracking()
            .Where(h => h.Activo && h.Sede.Activo && (h.Estado == "Disponible" || h.Estado == "Ocupada") &&
                h.PrecioNoche > 0 && h.PrecioNoche <= consulta.PresupuestoPorNoche &&
                h.TipoHabitacion.CapacidadAdultos >= consulta.Adultos)
            .Where(h => string.IsNullOrEmpty(ciudad) || h.Sede.Ciudad == ciudad)
            .Where(h => !context.ReservaHabitaciones.Any(rh => rh.HabitacionId == h.Id &&
                rh.Reserva.Estado != "Cancelada" && rh.FechaCheckIn < consulta.FechaSalida && rh.FechaCheckOut > consulta.FechaEntrada))
            .Select(h => new { h.Id, h.FotoUrl, h.SedeId, SedeNombre = h.Sede.Nombre, h.Sede.Ciudad, h.Sede.Categoria, h.PrecioNoche, Tipo = h.TipoHabitacion.Nombre })
            .ToListAsync(cancellationToken);

        var avisos = new List<string>
        {
            "Precios de una habitación para los adultos indicados, sin servicios adicionales. La disponibilidad se vuelve a verificar al reservar."
        };
        var candidatos = habitaciones.GroupBy(h => h.SedeId).Select(g => g.OrderBy(h => h.PrecioNoche).ThenBy(h => h.Id).First())
            .OrderBy(h => h.PrecioNoche).ThenBy(h => h.SedeId).ToList();
        if (candidatos.Count == 0)
            return new("Me encantaría ayudarte a encontrar una buena opción. Por ahora no hay habitaciones que coincidan con los datos de tu viaje. ¿Probamos otras fechas, una ciudad diferente o un poco más de presupuesto? Puedes ajustar los datos del viaje y volvemos a buscar.",
                false, DateTimeOffset.UtcNow, [], avisos);

        if (candidatos.Count > 12) avisos.Add("Se compararon las 12 sedes con menor precio entre las que cumplen los filtros.");
        candidatos = candidatos.Take(12).ToList();
        var ciudades = candidatos.Select(c => c.Ciudad).Distinct().ToList();
        var climas = await Task.WhenAll(ciudades.Select(c => weather.ConsultarAsync(c, consulta.FechaEntrada, consulta.FechaSalida, cancellationToken)));
        var climaPorCiudad = ciudades.Zip(climas).ToDictionary(x => x.First, x => x.Second);
        var noches = (consulta.FechaSalida - consulta.FechaEntrada).Days;
        var opciones = candidatos.Select(c => new EstadiaRecomendadaDto(c.SedeId, c.SedeNombre, c.Ciudad, c.Categoria,
            c.PrecioNoche, c.PrecioNoche * noches, noches, c.Tipo,
            "Esta habitación está disponible para tus fechas y se ajusta al presupuesto y al número de adultos. La seleccioné por precio; todavía no pude evaluar las preferencias de tu mensaje con IA.",
            climaPorCiudad[c.Ciudad], c.Id, c.FotoUrl)).ToList();

        var explicacion = await assistant.ExplicarAsync(consulta, opciones, cancellationToken);
        if (explicacion is null)
        {
            avisos.Add("La explicación con IA no está disponible en este momento. Mostramos las opciones del catálogo ordenadas por precio.");
            return new("¡Vamos a darle forma a tu próxima escapada! Encontré estas opciones disponibles para los datos de tu viaje. Hoy no pude conectar con la IA, así que las ordené por precio para ayudarte a compararlas. Puedes explorar sus fotos y el clima disponible antes de elegir.", false, DateTimeOffset.UtcNow, opciones.Take(3).ToList(), avisos);
        }
        var seleccion = explicacion.Destinos.Select(d => opciones.Single(o => o.SedeId == d.SedeId) with { Motivo = d.Motivo }).ToList();
        return new(explicacion.Resumen, true, DateTimeOffset.UtcNow, seleccion, avisos);
    }
}
