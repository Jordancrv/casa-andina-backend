using CasaAndina.Application.Chatbot.DTOs;
using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CasaAndina.Application.Chatbot.Queries;

public sealed record ConversarViajeQuery(string Mensaje, PreferenciasConversacionDto? Preferencias = null,
    IReadOnlyList<MensajeViajeDto>? Historial = null) : IRequest<ConversacionViajeDto>;

public sealed class ConversarViajeQueryValidator : AbstractValidator<ConversarViajeQuery>
{
    public ConversarViajeQueryValidator()
    {
        RuleFor(x => x.Mensaje).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Preferencias!.Ciudad).MaximumLength(100).When(x => x.Preferencias is not null);
        RuleFor(x => x.Historial).Must(h => h is null || (h.Count <= 8 && h.All(m => m is not null
            && (m.Rol is "usuario" or "asistente") && !string.IsNullOrWhiteSpace(m.Texto) && m.Texto.Length <= 1000)))
            .WithMessage("El historial admite hasta 8 mensajes de usuario o asistente de hasta 1000 caracteres.");
    }
}

public sealed class ConversarViajeQueryHandler(IApplicationDbContext db, ITravelConversationClient assistant, ISender sender)
    : IRequestHandler<ConversarViajeQuery, ConversacionViajeDto>
{
    public async Task<ConversacionViajeDto> Handle(ConversarViajeQuery request, CancellationToken cancellationToken)
    {
        var hoy = PeruBusinessTime.Today;
        var ciudades = await db.Sedes.AsNoTracking().Where(s => s.Activo).Select(s => s.Ciudad)
            .Distinct().OrderBy(c => c).ToListAsync(cancellationToken);
        var known = ConversacionViaje.Normalizar(request.Preferencias ?? new(), ciudades, hoy);
        var fallbackInicial = ConversacionViaje.LeerMensaje(request.Mensaje, known, ciudades, hoy);
        var ciudadesMencionadas = ConversacionViaje.CiudadesEnMensaje(request.Mensaje, ciudades);
        const string meses = @"\b(enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre)\b";
        var mes = Regex.Match(request.Mensaje, meses, RegexOptions.IgnoreCase);
        var textoFecha = request.Mensaje;
        if (mes.Success) fallbackInicial = fallbackInicial with { MesReferencia = mes.Value.ToLowerInvariant() };
        else if (known.MesReferencia is { } referencia && Regex.IsMatch(referencia, "^" + meses + "$", RegexOptions.IgnoreCase)
            && Regex.IsMatch(textoFecha, @"\b\d{1,2}\s+al\s+\d{1,2}\s*[.!?]?$"))
            textoFecha = textoFecha.TrimEnd(' ', '.', '!', '?') + " de " + referencia;
        var fechas = ConversacionViaje.FechasExplicitas(textoFecha, hoy);
        if (fechas.Error is not null)
            return new(fechas.Error, false, fallbackInicial with { FechaEntrada = null, FechaSalida = null }, null, []);
        if (fechas.Entrada.HasValue)
            fallbackInicial = fallbackInicial with { FechaEntrada = fechas.Entrada, FechaSalida = fechas.Salida ?? (fechas.Entrada == known.FechaEntrada ? known.FechaSalida : null) };
        var playas = ciudades.Where(c => Regex.IsMatch(c, @"^(Chiclayo|Piura|Lima|Trujillo)\b", RegexOptions.IgnoreCase)).ToArray();
        var destinoExplicito = Regex.Match(request.Mensaje.Trim(),
            @"\b(?:viajar a|irme a|hospedarme en|alojarme en|reservar en|habitaci[oó]n en|hotel(?:es)? en|sede en|ir a)\s+([\p{L}]+(?:\s+[\p{L}]+){0,4}?)(?=\s+(?:el|del|para|por|desde|con|en|dentro)\b|\s*[,.!?]|$)",
            RegexOptions.IgnoreCase);
        if (ciudadesMencionadas.Count == 0 && destinoExplicito.Success &&
            !Regex.IsMatch(destinoExplicito.Groups[1].Value, @"\b(playas?|costa|sierra|selva|alg[uú]n|una?|la|otra?|cualquier|per[uú])\b", RegexOptions.IgnoreCase))
            return new($"No tenemos sede en {destinoExplicito.Groups[1].Value}. Contamos con {string.Join(", ", ciudades)}. ¿Te ayudo a encontrar una alternativa similar?", false, fallbackInicial, null, ciudades);
        var pendienteCiudad = request.Historial?.LastOrDefault() is { Rol: "asistente", Texto: "¿En qué ciudad te gustaría conocer atractivos?" };
        var contextoAtractivos = request.Historial?.LastOrDefault() is { Rol: "asistente" } ultimo &&
            ultimo.Texto.StartsWith("Estos son algunos lugares para explorar", StringComparison.Ordinal);
        if (fallbackInicial.PrefierePlaya && ciudadesMencionadas.Count == 0 && (fallbackInicial.Ciudad == null || !playas.Contains(fallbackInicial.Ciudad)))
        {
            var mensaje = playas.Length > 0
                ? $"Para una escapada a la playa puedes alojarte en {string.Join(", ", playas)} y visitar playas de sus alrededores. ¿Qué ciudad prefieres?"
                : $"No tenemos destinos de playa en el catálogo actual. Contamos con {string.Join(", ", ciudades)}. ¿Te ayudo a elegir una alternativa?";
            return new(mensaje, false, fallbackInicial with { Ciudad = null, DestinoFlexible = false }, null, playas);
        }
        if (AtractivosViaje.EsConsulta(request.Mensaje) || (pendienteCiudad && ciudadesMencionadas.Count > 0) || (contextoAtractivos && ciudadesMencionadas.Count > 0))
        {
            var ciudad = ciudadesMencionadas.FirstOrDefault() ?? known.Ciudad;
            var guias = (ciudadesMencionadas.Count > 0 ? ciudadesMencionadas : ciudad is null ? [] : new[] { ciudad })
                .Select(AtractivosViaje.Buscar).Where(a => a is not null).Cast<AtractivosCiudadDto>().ToArray();
            var atractivos = guias.FirstOrDefault();
            var mensaje = atractivos is not null
                ? $"Estos son algunos lugares para explorar en {string.Join(" y ", guias.Select(g => g.Ciudad))} y sus alrededores. Revisa horarios, costos y traslados antes de visitarlos."
                : ciudad is null ? "¿En qué ciudad te gustaría conocer atractivos?" : $"Todavía no tengo una guía verificada para {ciudad}. Puedo ayudarte a elegir una habitación o consultar otra ciudad.";
            if (guias.Length < ciudadesMencionadas.Count) mensaje += " No tengo una guía verificada para todas las ciudades solicitadas.";
            return new(mensaje, false, known, null, ciudad is null
                ? ciudades.Take(6).Select(c => $"¿Qué atractivos puedo visitar en {c}?").ToArray() : [], atractivos, guias.Skip(1).ToArray());
        }
        if (Regex.IsMatch(request.Mensaje, @"\b(maleta|mochila|equipaje|vestimenta|empacar|llevar ropa)\b", RegexOptions.IgnoreCase))
            return new("Puedo ayudarte a comparar sedes y habitaciones, revisar el clima de tu estadía o conocer atractivos de tu destino.", false, known, null, ["Ayúdame a buscar habitaciones"]);
        if (ciudadesMencionadas.Count > 1)
            return new("Por ahora puedo buscar habitaciones en una ciudad a la vez o en todos los destinos. ¿Cuál prefieres? Conservaré las fechas, los adultos y el presupuesto que ya indicaste.", false, known, null,
                ciudadesMencionadas.Select(c => $"Buscar habitaciones en {c}").Append("Buscar en cualquier destino").ToArray());
        var confirmaOferta = request.Historial?.LastOrDefault() is { Rol: "asistente" } propuesta && propuesta.Texto.Contains("¿Te muestro las opciones desde S/", StringComparison.Ordinal);
        var aceptaTarifa = known.TarifaOfrecida.HasValue && fallbackInicial.PresupuestoPorNoche == known.PresupuestoPorNoche &&
            Regex.IsMatch(request.Mensaje.Trim(), @"^(?:ya\s+)?(?:me adapto a ese presupuesto|acepto(?: ese precio| esa tarifa)?|est[aá] bien(?: ese precio)?)[\s.!]*$", RegexOptions.IgnoreCase) || (known.TarifaOfrecida.HasValue && confirmaOferta && Regex.IsMatch(request.Mensaje.Trim(), @"^s[ií][.!]*$", RegexOptions.IgnoreCase));
        if (aceptaTarifa) fallbackInicial = fallbackInicial with { PresupuestoPorNoche = known.TarifaOfrecida, TarifaOfrecida = null };
        var safeRequest = request with { Preferencias = known };
        var interpreted = aceptaTarifa ? null : await assistant.InterpretarAsync(safeRequest, ciudades, hoy, cancellationToken);
        if (!string.IsNullOrWhiteSpace(interpreted?.DestinoNoDisponible))
            return new($"No tenemos sede en {interpreted.DestinoNoDisponible}. Contamos con {string.Join(", ", ciudades)}. ¿Te ayudo a elegir una opción similar?", false, fallbackInicial, null, ciudades);
        var fallback = fallbackInicial;
        var parsed = interpreted?.Preferencias;
        var merged = parsed is null ? fallback : parsed with
        {
            FechaEntrada = parsed.FechaEntrada ?? fallback.FechaEntrada,
            FechaSalida = parsed.FechaSalida ?? fallback.FechaSalida,
            PresupuestoPorNoche = fallback.PresupuestoPorNoche != known.PresupuestoPorNoche ? fallback.PresupuestoPorNoche : parsed.PresupuestoPorNoche ?? fallback.PresupuestoPorNoche,
            Adultos = fallback.Adultos != known.Adultos ? fallback.Adultos : parsed.Adultos ?? fallback.Adultos,
            Ciudad = ciudadesMencionadas.Count == 1 ? ciudadesMencionadas[0] : parsed.Ciudad ?? (parsed.DestinoFlexible ? null : fallback.Ciudad),
            DestinoFlexible = ciudadesMencionadas.Count == 1 ? false : parsed.DestinoFlexible || fallback.DestinoFlexible,
            MesReferencia = fallback.MesReferencia,
            TarifaOfrecida = null,
            PrefierePlaya = fallback.PrefierePlaya,
            EvitarLluvia = fallback.EvitarLluvia
        };
        if (ciudadesMencionadas.Count == 1 && known.Ciudad != ciudadesMencionadas[0])
            merged = merged with { FechaEntrada = fallback.FechaEntrada, FechaSalida = fallback.FechaSalida,
                Adultos = fallback.Adultos, PresupuestoPorNoche = fallback.PresupuestoPorNoche };
        if (fechas.Entrada.HasValue)
            merged = merged with { FechaEntrada = fechas.Entrada, FechaSalida = fechas.Salida ?? merged.FechaSalida };
        var preferences = ConversacionViaje.Normalizar(merged, ciudades, hoy);
        var missing = ConversacionViaje.Faltante(preferences);
        if (missing is not null)
        {
            var modelo = parsed is null ? null : parsed with { MesReferencia = preferences.MesReferencia, PrefierePlaya = preferences.PrefierePlaya, TarifaOfrecida = null };
            var message = modelo == preferences ? interpreted?.Mensaje : null;
            return new(string.IsNullOrWhiteSpace(message) ? ConversacionViaje.Preguntar(missing) : message,
                interpreted is not null, preferences, null, ConversacionViaje.Sugerencias(missing, ciudades));
        }
        // No se hacen búsquedas de inventario para un saludo o una respuesta ajena al viaje.
        var recienCompletado = ConversacionViaje.Faltante(known) is not null;
        var aclaraPresupuesto = Regex.IsMatch(request.Mensaje, @"\b(presupuesto|soles|por noche)\b", RegexOptions.IgnoreCase)
            && Regex.IsMatch(request.Mensaje, @"\d");
        var solicitaBusqueda = Regex.IsMatch(request.Mensaje, @"\b(opciones|habitaciones|buscar|busca|mu[eé]strame|comparar)\b", RegexOptions.IgnoreCase);
        var cambiaCiudad = ciudadesMencionadas.Count == 1 && known.Ciudad != ciudadesMencionadas[0];
        if ((!recienCompletado && !aclaraPresupuesto && !solicitaBusqueda && !cambiaCiudad && !(ciudadesMencionadas.Count == 1 && Regex.IsMatch(request.Mensaje, @"^s[ií]\b", RegexOptions.IgnoreCase)) && interpreted is { BuscarOpciones: false }) || (interpreted is null &&
            Regex.IsMatch(request.Mensaje.Trim(), @"^[¡!¿?\s]*(hola|buenas|gracias|muchas gracias|buenos d[ií]as|buenas tardes|buenas noches)[!.?\s]*$", RegexOptions.IgnoreCase)))
            return new(interpreted?.Mensaje ?? "¡Qué gusto seguir conversando contigo! Ya tengo los datos de tu viaje. ¿Qué te gustaría saber?",
                interpreted is not null, preferences, null, ["Muéstrame las opciones", "¿Qué atractivos puedo visitar?"]);
        var query = new PlanificarViajeQuery(request.Mensaje, preferences.FechaEntrada!.Value, preferences.FechaSalida!.Value,
            preferences.PresupuestoPorNoche!.Value, preferences.Adultos!.Value, preferences.Ciudad, request.Historial, preferences.EvitarLluvia);
        var plan = await sender.Send(query, cancellationToken);
        if (plan.TarifaAlternativa is decimal tarifa && tarifa > preferences.PresupuestoPorNoche)
            return new(plan.Resumen + $" ¿Te muestro las opciones desde S/ {tarifa:0.##} por noche?", plan.UsaIa,
                preferences with { TarifaOfrecida = tarifa }, plan, ["Acepto esa tarifa", "Quiero cambiar las fechas"]);
        return new(plan.Resumen, plan.UsaIa, preferences with { TarifaOfrecida = null }, plan,
            plan.FiltroLluvia && !plan.Recomendaciones.Any(o => o.ClimaFavorable)
                ? ["Quiero cambiar las fechas", "Ayúdame a elegir el destino"]
                : ["¿Qué atractivos puedo visitar?", "Ayúdame a comparar las opciones", "Quiero cambiar las fechas"]);
    }
}
