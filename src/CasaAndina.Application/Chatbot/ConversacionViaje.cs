using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CasaAndina.Application.Chatbot.DTOs;

namespace CasaAndina.Application.Chatbot;

/// <summary>Respaldo acotado cuando Gemini no responde; nunca completa un viaje con valores supuestos.</summary>
public static class ConversacionViaje
{
    public static PreferenciasConversacionDto Normalizar(PreferenciasConversacionDto p, IReadOnlyList<string> ciudades, DateTime hoy)
    {
        var entrada = p.FechaEntrada is { } e && e.TimeOfDay == TimeSpan.Zero && e >= hoy && e <= hoy.AddDays(365) ? p.FechaEntrada : null;
        var salida = entrada.HasValue && p.FechaSalida is { } s && s.TimeOfDay == TimeSpan.Zero && s > entrada && s <= entrada.Value.AddDays(30) ? p.FechaSalida : null;
        var ciudad = ciudades.FirstOrDefault(c => Normal(c) == Normal(p.Ciudad ?? ""));
        return new(entrada, salida, p.PresupuestoPorNoche is >= 1 and <= 10000 ? p.PresupuestoPorNoche : null,
            p.Adultos is >= 1 and <= 8 ? p.Adultos : null, ciudad, ciudad is null && p.DestinoFlexible);
    }
    public static string? Faltante(PreferenciasConversacionDto p) => p.Ciudad is null && !p.DestinoFlexible ? "ciudad"
        : p.FechaEntrada is null || p.FechaSalida is null ? "fechas" : p.PresupuestoPorNoche is null ? "presupuesto"
        : p.Adultos is null ? "adultos" : null;
    public static string Preguntar(string field) => field switch
    {
        "ciudad" => "¡Me encantará ayudarte a preparar esa escapada! ¿Tienes alguna ciudad en mente o prefieres que te ayude a elegir?",
        "fechas" => "¡Vamos dándole forma al viaje! ¿Qué día te gustaría llegar y cuándo saldrías? Puedes escribir las fechas o decirme, por ejemplo, «desde mañana, por dos noches».",
        "presupuesto" => "¿Cuánto te gustaría gastar como máximo por noche, en soles? Así puedo buscar opciones que se ajusten a ti.",
        _ => "Solo me falta saber cuántos adultos viajarían. ¿Vas por tu cuenta, en pareja o con más personas?"
    };
    public static IReadOnlyList<string> Sugerencias(string field, IReadOnlyList<string> ciudades) => field switch
    {
        "ciudad" => ciudades.Take(3).Append("Ayúdame a elegir el destino").ToArray(),
        "fechas" => ["Desde mañana, por 2 noches", "Todavía estoy eligiendo las fechas"],
        "presupuesto" => ["Hasta S/ 250 por noche", "Hasta S/ 500 por noche", "Hasta S/ 800 por noche"],
        _ => ["Viajo solo", "Viajamos en pareja", "Somos 3 adultos"]
    };
    public static PreferenciasConversacionDto LeerMensaje(string message, PreferenciasConversacionDto p, IReadOnlyList<string> ciudades, DateTime hoy)
    {
        var text = Normal(message);
        if (Regex.IsMatch(text, @"\b(cambiar|cambiemos|otras)\b.*\bfechas\b")) p = p with { FechaEntrada = null, FechaSalida = null };
        if (Regex.IsMatch(text, @"\b(cambiar|cambiemos|otro)\b.*\bpresupuesto\b")) p = p with { PresupuestoPorNoche = null };
        if (p.Ciudad is { } previousCity && Regex.IsMatch(text, $@"\b(no quiero|evitar|menos|excepto)\b.{{0,20}}\b{Regex.Escape(Normal(previousCity.Split('(')[0].Trim()))}\b"))
            p = p with { Ciudad = null, DestinoFlexible = false };
        var ciudad = ciudades.OrderByDescending(c => c.Length).FirstOrDefault(c =>
            Regex.IsMatch(text, $@"\b{Regex.Escape(Normal(c.Split('(')[0].Trim()))}\b")
            && !Regex.IsMatch(text, $@"\b(no quiero|evitar|menos|excepto)\b.{{0,20}}\b{Regex.Escape(Normal(c.Split('(')[0].Trim()))}\b"));
        if (ciudad is not null) p = p with { Ciudad = ciudad, DestinoFlexible = false };
        if (Regex.IsMatch(text, "(ayudame a elegir|cualquier (ciudad|destino)|sin preferencia de (ciudad|destino))"))
            p = p with { Ciudad = null, DestinoFlexible = true };
        var dates = Regex.Matches(text, @"\b\d{4}-\d{2}-\d{2}\b").Select(m =>
            DateTime.TryParseExact(m.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? (DateTime?)date : null).ToArray();
        if (dates.Length > 0) p = p with { FechaEntrada = dates[0], FechaSalida = dates.Length > 1 ? dates[1] : null };
        if (Regex.IsMatch(text, @"\b(desde |a partir de )?manana\b")) p = p with { FechaEntrada = hoy.AddDays(1), FechaSalida = null };
        var nights = Regex.Match(text, @"\b(\d{1,2}|una?|dos|tres|cuatro|cinco) noches?\b");
        if (nights.Success && p.FechaEntrada is { } start)
        {
            var words = new Dictionary<string, int> { ["un"] = 1, ["una"] = 1, ["dos"] = 2, ["tres"] = 3, ["cuatro"] = 4, ["cinco"] = 5 };
            var count = int.TryParse(nights.Groups[1].Value, out var n) ? n : words[nights.Groups[1].Value];
            p = p with { FechaSalida = start.AddDays(count) };
        }
        var budget = Regex.Match(text, @"(?:s/\.?\s*|presupuesto(?: de| hasta)?\s*)(\d{1,5}(?:[.,]\d{1,2})?)");
        if (!budget.Success) budget = Regex.Match(text, @"\b(\d{1,5}(?:[.,]\d{1,2})?)\s*soles\b");
        if (budget.Success && decimal.TryParse(budget.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            p = p with { PresupuestoPorNoche = amount };
        var adults = Regex.Match(text, @"\b(\d{1,2}) (adultos?|personas?)\b");
        if (adults.Success) p = p with { Adultos = int.Parse(adults.Groups[1].Value, CultureInfo.InvariantCulture) };
        else if (Regex.IsMatch(text, @"\b(pareja|somos dos)\b")) p = p with { Adultos = 2 };
        else if (Regex.IsMatch(text, @"\b(viajo solo|viajo sola|solo yo)\b")) p = p with { Adultos = 1 };
        if (Regex.IsMatch(text, @"^\d{1,5}(?:[.,]\d{1,2})?$"))
        {
            if (Faltante(p) == "presupuesto") p = p with { PresupuestoPorNoche = decimal.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture) };
            else if (Faltante(p) == "adultos" && int.TryParse(text, out var count)) p = p with { Adultos = count };
        }
        return p;
    }
    private static string Normal(string text) => string.Concat(text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
}
