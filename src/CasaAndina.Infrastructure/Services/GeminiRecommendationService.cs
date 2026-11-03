using CasaAndina.Application.Chatbot.DTOs;
using CasaAndina.Application.Chatbot.Queries;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common;
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
        if (habitaciones.Count == 0)
        {
            // El listado de reserva muestra toda la disponibilidad; aquí explicamos los filtros
            // adicionales del asistente con datos reales para las mismas fechas y ciudad.
            var disponibles = await context.Habitaciones.AsNoTracking()
                .Where(h => h.Activo && h.Sede.Activo && (h.Estado == "Disponible" || h.Estado == "Ocupada") && h.PrecioNoche > 0)
                .Where(h => string.IsNullOrEmpty(ciudad) || h.Sede.Ciudad == ciudad)
                .Where(h => !context.ReservaHabitaciones.Any(rh => rh.HabitacionId == h.Id && rh.Reserva.Estado != "Cancelada" &&
                    rh.FechaCheckIn < consulta.FechaSalida && rh.FechaCheckOut > consulta.FechaEntrada))
                .Select(h => new { h.PrecioNoche, Capacidad = h.TipoHabitacion.CapacidadAdultos })
                .ToListAsync(cancellationToken);
            var dentroPresupuesto = disponibles.Where(h => h.PrecioNoche <= consulta.PresupuestoPorNoche).ToList();
            var conCapacidad = disponibles.Where(h => h.Capacidad >= consulta.Adultos).ToList();
            string mensaje;
            if (disponibles.Count == 0)
                mensaje = "No encontré habitaciones disponibles para esas fechas en el destino seleccionado. Podemos probar otras fechas o ciudades.";
            else if (dentroPresupuesto.Count > 0 && dentroPresupuesto.All(h => h.Capacidad < consulta.Adultos))
            {
                var capacidad = dentroPresupuesto.Max(h => h.Capacidad);
                mensaje = $"Sí hay habitaciones disponibles dentro de tu presupuesto, pero admiten hasta {capacidad} {(capacidad == 1 ? "adulto" : "adultos")} por habitación. Busqué una sola habitación para los {consulta.Adultos} adultos indicados.";
                if (conCapacidad.Count > 0)
                    mensaje += $" Las habitaciones disponibles con capacidad suficiente cuestan desde S/ {conCapacidad.Min(h => h.PrecioNoche):0.##} por noche.";
                mensaje += " Puedes revisar dos habitaciones y su precio total, o cambiar las fechas, el destino o el presupuesto.";
            }
            else if (conCapacidad.Count > 0)
                mensaje = $"Hay habitaciones disponibles para {consulta.Adultos} adultos, pero cuestan desde S/ {conCapacidad.Min(h => h.PrecioNoche):0.##} por noche y superan tu presupuesto de S/ {consulta.PresupuestoPorNoche:0.##}. Podemos cambiar el presupuesto, las fechas o el destino.";
            else
                mensaje = $"Hay habitaciones disponibles, pero ninguna tiene capacidad para {consulta.Adultos} adultos en una sola habitación. Puedes revisar dos habitaciones o cambiar las fechas o el destino.";
            return new(mensaje, false, DateTimeOffset.UtcNow, [], avisos);
        }

        var desde = PeruBusinessTime.UtcStartOfDay(PeruBusinessTime.Today.AddDays(-90));
        var popularidad = await context.Reservas.AsNoTracking()
            .Where(r => r.FechaCreacion >= desde && r.FechaCreacion < DateTime.UtcNow && (r.Estado == "Reservado" || r.Estado == "Completada"))
            .GroupBy(r => r.SedeId).Select(g => new { SedeId = g.Key, Cantidad = g.Count() }).ToDictionaryAsync(x => x.SedeId, x => x.Cantidad, cancellationToken);
        var candidatos = habitaciones
            .OrderByDescending(h => popularidad.GetValueOrDefault(h.SedeId)).ThenBy(h => h.PrecioNoche).ThenBy(h => h.SedeId).ToList();
        var ciudades = candidatos.Select(c => c.Ciudad).Distinct().ToList();
        var climas = await Task.WhenAll(ciudades.Select(c => weather.ConsultarAsync(c, consulta.FechaEntrada, consulta.FechaSalida, cancellationToken)));
        var climaPorCiudad = ciudades.Zip(climas).ToDictionary(x => x.First, x => x.Second);
        var noches = (consulta.FechaSalida - consulta.FechaEntrada).Days;
        var opciones = candidatos.Select(c => new EstadiaRecomendadaDto(c.SedeId, c.SedeNombre, c.Ciudad, c.Categoria,
            c.PrecioNoche, c.PrecioNoche * noches, noches, c.Tipo,
            "Esta habitación está disponible para tus fechas y se ajusta al presupuesto y al número de adultos.",
            climaPorCiudad[c.Ciudad], c.Id, c.FotoUrl, popularidad.GetValueOrDefault(c.SedeId))).ToList();

        static (double Riesgo, bool Completo, bool PrecipitacionPrevista, string? Motivo) EvaluarClima(EstadiaRecomendadaDto opcion, DateTime entrada, DateTime salida)
        {
            var dias = opcion.Clima.Dias.Where(d => DateTime.TryParse(d.Fecha, out var fecha) && fecha.Date >= entrada.Date && fecha.Date < salida.Date).ToArray();
            var completo = dias.Select(d => d.Fecha).Distinct().Count() == (salida.Date - entrada.Date).Days
                && dias.All(d => d.ProbabilidadLluvia.HasValue && d.CodigoTiempo.HasValue);
            var diaConPrecipitacion = dias.FirstOrDefault(d => d.CodigoTiempo is >= 51 and <= 67 or >= 71 and <= 77 or >= 80 and <= 86 or >= 95 and <= 99);
            if (!completo) return (double.MaxValue, false, diaConPrecipitacion is not null, "Faltan datos de probabilidad o condición meteorológica para uno o más días de la estadía.");
            var riesgo = dias.Max(d => d.ProbabilidadLluvia!.Value);
            if (diaConPrecipitacion is not null)
                return (riesgo, true, true, $"El pronóstico indica precipitación para el {diaConPrecipitacion.Fecha}, aunque la probabilidad sea baja.");
            if (riesgo > 20)
                return (riesgo, true, false, $"La probabilidad máxima diaria de precipitación es {riesgo:0.#} %, superior al límite orientativo de 20 %.");
            return (riesgo, true, false, null);
        }
        if (consulta.EvitarLluvia)
        {
            // Criterio orientativo, no garantía: hasta 20 % y sin lluvia, nieve o tormenta prevista durante las noches reservadas.
            opciones = opciones.Select(o => { var c = EvaluarClima(o, consulta.FechaEntrada, consulta.FechaSalida); return o with { ClimaFavorable = c.Completo && !c.PrecipitacionPrevista && c.Riesgo <= 20, MotivoFiltroClima = c.Motivo }; })
                .OrderByDescending(o => o.ClimaFavorable).ThenBy(o => EvaluarClima(o, consulta.FechaEntrada, consulta.FechaSalida).Riesgo)
                .ThenByDescending(o => o.ReservasRecientes).ThenBy(o => o.PrecioPorNoche).ToList();
            avisos.Add("Criterio orientativo: pronóstico completo para los días de las noches reservadas, sin precipitación prevista y con una probabilidad máxima diaria de hasta 20 %. El día de salida se muestra como información adicional. El pronóstico puede cambiar y no garantiza ausencia de lluvia.");
            if (!opciones.Any(o => o.ClimaFavorable))
            {
                var sinDatos = opciones.All(o => !EvaluarClima(o, consulta.FechaEntrada, consulta.FechaSalida).Completo);
                return new(sinDatos
                    ? "No tengo un pronóstico completo para comparar la lluvia en estas fechas. Puedes revisar todas las opciones o probar fechas más cercanas."
                    : "No encontré habitaciones con un pronóstico suficientemente favorable para evitar la lluvia. Puedes cambiar las fechas o el destino, o revisar todas las opciones.",
                    false, DateTimeOffset.UtcNow, opciones, avisos, true);
            }
        }
        var opcionesParaIa = opciones.Where(o => !consulta.EvitarLluvia || o.ClimaFavorable).GroupBy(o => o.SedeId).Select(g => g.First()).Take(12).ToList();
        var explicacion = await assistant.ExplicarAsync(consulta, opcionesParaIa, cancellationToken);
        if (explicacion is null)
        {
            avisos.Add(consulta.EvitarLluvia ? "Sin explicación de IA: se muestran los datos disponibles del pronóstico y del sistema." : "Sin explicación de IA: opciones ordenadas por reservas registradas en los últimos 90 días y precio, entre las sedes que cumplen tu búsqueda.");
            return new(consulta.EvitarLluvia ? "Encontré estas opciones con menor probabilidad de precipitación para tus fechas. El pronóstico puede cambiar." : "Encontré estas habitaciones disponibles para tu viaje.", false, DateTimeOffset.UtcNow, opciones, avisos, consulta.EvitarLluvia);
        }
        var seleccion = opciones.Select(o => o with { Motivo = opcionesParaIa.Any(p => p.HabitacionId == o.HabitacionId) ? explicacion.Destinos.FirstOrDefault(d => d.SedeId == o.SedeId)?.Motivo ?? o.Motivo : o.Motivo }).ToList();
        if (!consulta.EvitarLluvia) seleccion = seleccion.OrderBy(o => { var i = explicacion.Destinos.ToList().FindIndex(d => d.SedeId == o.SedeId); return i < 0 ? int.MaxValue : i; }).ToList();
        return new(consulta.EvitarLluvia ? explicacion.Resumen + " El pronóstico puede cambiar y no garantiza ausencia de lluvia." : explicacion.Resumen, true, DateTimeOffset.UtcNow, seleccion, avisos, consulta.EvitarLluvia);
    }
}
