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
        var safeRequest = request with { Preferencias = known };
        var interpreted = await assistant.InterpretarAsync(safeRequest, ciudades, hoy, cancellationToken);
        var fallback = ConversacionViaje.LeerMensaje(request.Mensaje, known, ciudades, hoy);
        var parsed = interpreted?.Preferencias;
        var merged = parsed is null ? fallback : parsed with
        {
            FechaEntrada = parsed.FechaEntrada ?? fallback.FechaEntrada,
            FechaSalida = parsed.FechaSalida ?? fallback.FechaSalida,
            PresupuestoPorNoche = parsed.PresupuestoPorNoche ?? fallback.PresupuestoPorNoche,
            Adultos = parsed.Adultos ?? fallback.Adultos,
            Ciudad = parsed.Ciudad ?? (parsed.DestinoFlexible ? null : fallback.Ciudad),
            DestinoFlexible = parsed.DestinoFlexible || fallback.DestinoFlexible
        };
        var preferences = ConversacionViaje.Normalizar(merged, ciudades, hoy);
        var missing = ConversacionViaje.Faltante(preferences);
        if (missing is not null)
        {
            var message = interpreted?.Preferencias == preferences ? interpreted.Mensaje : null;
            return new(string.IsNullOrWhiteSpace(message) ? ConversacionViaje.Preguntar(missing) : message,
                interpreted is not null, preferences, null, ConversacionViaje.Sugerencias(missing, ciudades));
        }
        // No se hacen búsquedas de inventario para un saludo o una respuesta ajena al viaje.
        if (interpreted is { BuscarOpciones: false } || (interpreted is null &&
            Regex.IsMatch(request.Mensaje.Trim(), @"^[¡!¿?\s]*(hola|buenas|gracias|muchas gracias|buenos d[ií]as|buenas tardes|buenas noches)[!.?\s]*$", RegexOptions.IgnoreCase)))
            return new(interpreted?.Mensaje ?? "¡Qué gusto seguir conversando contigo! Ya tengo los datos de tu viaje. ¿Qué te gustaría saber?",
                interpreted is not null, preferences, null, ["Muéstrame las opciones", "Comparemos el clima"]);
        var query = new PlanificarViajeQuery(request.Mensaje, preferences.FechaEntrada!.Value, preferences.FechaSalida!.Value,
            preferences.PresupuestoPorNoche!.Value, preferences.Adultos!.Value, preferences.Ciudad, request.Historial);
        var plan = await sender.Send(query, cancellationToken);
        return new(plan.Resumen, plan.UsaIa, preferences, plan,
            ["¿Qué llevar en la maleta?", "Ayúdame a comparar las opciones", "Quiero cambiar las fechas"]);
    }
}
