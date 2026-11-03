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
        var pendienteCiudad = request.Historial?.LastOrDefault() is { Rol: "asistente", Texto: "¿En qué ciudad te gustaría conocer atractivos?" };
        var contextoAtractivos = request.Historial?.LastOrDefault() is { Rol: "asistente" } ultimo &&
            ultimo.Texto.StartsWith("Estos son algunos lugares para explorar", StringComparison.Ordinal);
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
        var safeRequest = request with { Preferencias = known };
        var interpreted = await assistant.InterpretarAsync(safeRequest, ciudades, hoy, cancellationToken);
        var fallback = fallbackInicial;
        var parsed = interpreted?.Preferencias;
        var merged = parsed is null ? fallback : parsed with
        {
            FechaEntrada = parsed.FechaEntrada ?? fallback.FechaEntrada,
            FechaSalida = parsed.FechaSalida ?? fallback.FechaSalida,
            PresupuestoPorNoche = parsed.PresupuestoPorNoche ?? fallback.PresupuestoPorNoche,
            Adultos = fallback.Adultos != known.Adultos ? fallback.Adultos : parsed.Adultos ?? fallback.Adultos,
            Ciudad = ciudadesMencionadas.Count == 1 ? ciudadesMencionadas[0] : parsed.Ciudad ?? (parsed.DestinoFlexible ? null : fallback.Ciudad),
            DestinoFlexible = ciudadesMencionadas.Count == 1 ? false : parsed.DestinoFlexible || fallback.DestinoFlexible,
            EvitarLluvia = fallback.EvitarLluvia
        };
        if (ciudadesMencionadas.Count == 1 && known.Ciudad != ciudadesMencionadas[0])
            merged = merged with { FechaEntrada = fallback.FechaEntrada, FechaSalida = fallback.FechaSalida,
                Adultos = fallback.Adultos, PresupuestoPorNoche = fallback.PresupuestoPorNoche };
        var preferences = ConversacionViaje.Normalizar(merged, ciudades, hoy);
        var missing = ConversacionViaje.Faltante(preferences);
        if (missing is not null)
        {
            var message = interpreted?.Preferencias == preferences ? interpreted.Mensaje : null;
            return new(string.IsNullOrWhiteSpace(message) ? ConversacionViaje.Preguntar(missing) : message,
                interpreted is not null, preferences, null, ConversacionViaje.Sugerencias(missing, ciudades));
        }
        // No se hacen búsquedas de inventario para un saludo o una respuesta ajena al viaje.
        var recienCompletado = ConversacionViaje.Faltante(known) is not null;
        var aclaraPresupuesto = Regex.IsMatch(request.Mensaje, @"\b(presupuesto|soles|por noche)\b", RegexOptions.IgnoreCase)
            && Regex.IsMatch(request.Mensaje, @"\d");
        var solicitaBusqueda = Regex.IsMatch(request.Mensaje, @"\b(opciones|habitaciones|buscar|busca|mu[eé]strame|comparar)\b", RegexOptions.IgnoreCase);
        var cambiaCiudad = ciudadesMencionadas.Count == 1 && known.Ciudad != ciudadesMencionadas[0];
        if ((!recienCompletado && !aclaraPresupuesto && !solicitaBusqueda && !cambiaCiudad && interpreted is { BuscarOpciones: false }) || (interpreted is null &&
            Regex.IsMatch(request.Mensaje.Trim(), @"^[¡!¿?\s]*(hola|buenas|gracias|muchas gracias|buenos d[ií]as|buenas tardes|buenas noches)[!.?\s]*$", RegexOptions.IgnoreCase)))
            return new(interpreted?.Mensaje ?? "¡Qué gusto seguir conversando contigo! Ya tengo los datos de tu viaje. ¿Qué te gustaría saber?",
                interpreted is not null, preferences, null, ["Muéstrame las opciones", "Comparemos el clima"]);
        var query = new PlanificarViajeQuery(request.Mensaje, preferences.FechaEntrada!.Value, preferences.FechaSalida!.Value,
            preferences.PresupuestoPorNoche!.Value, preferences.Adultos!.Value, preferences.Ciudad, request.Historial, preferences.EvitarLluvia);
        var plan = await sender.Send(query, cancellationToken);
        return new(plan.Resumen, plan.UsaIa, preferences, plan,
            plan.FiltroLluvia && !plan.Recomendaciones.Any(o => o.ClimaFavorable)
                ? ["Quiero cambiar las fechas", "Ayúdame a elegir el destino"]
                : ["¿Qué atractivos puedo visitar?", "Ayúdame a comparar las opciones", "Quiero cambiar las fechas"]);
    }
}
