using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CasaAndina.Application.Chatbot.DTOs;

namespace CasaAndina.Application.Chatbot.Queries;

// Catálogo editorial: no inventa precios, horarios ni distancias.
internal static class AtractivosViaje
{
    private static readonly Dictionary<string, (string[] Lugares, string Fuente)> Catalogo = new()
    {
        ["arequipa"] = (["Centro histórico de Arequipa", "Monasterio de Santa Catalina", "Mirador de Yanahuara"], "https://www.peru.travel/es/destinos/arequipa"),
        ["cusco"] = (["Centro histórico de Cusco", "Mercado San Pedro", "Barrio San Blas"], "https://www.peru.travel/es/destinos/cusco"),
        ["lima"] = (["Plaza Mayor de Lima", "Jirón de la Unión", "Plaza San Martín"], "https://www.peru.travel/es/experiencias/recorrido-centro-historico-de-lima"),
        ["chiclayo"] = (["Museo Tumbas Reales de Sipán", "Pirámides de Túcume", "Complejo arqueológico Chotuna"], "https://www.peru.travel/es/experiencias/sitios-arqueologicos-en-chiclayo-y-alrededores"),
        ["nasca"] = (["Líneas de Nasca", "Acueductos de Cantalloc", "Cahuachi"], "https://www.peru.travel/es/atractivos/lineas-de-nasca"),
        ["trujillo"] = (["Plaza de Armas de Trujillo", "Catedral de Trujillo", "Museo del Juguete"], "https://www.peru.travel/es/atractivos/centro-historico-de-trujillo"),
        ["huaraz"] = (["Parque Nacional Huascarán", "Laguna Chinancocha, Llanganuco"], "https://www.peru.travel/es/atractivos/parque-nacional-huascaran"),
        ["huancayo"] = (["Plaza de la Constitución de Huancayo", "Parque de la Identidad Huanca", "Formaciones geológicas de Torre Torre"], "https://target2.promperu.gob.pe/destinos/junin/huancayo"),
        ["piura"] = (["Plaza de Armas de Piura", "Catedral de Piura", "Casa Museo Almirante Miguel Grau"], "https://www.peru.travel/es/inspirate/descubre-peru-los-5-destinos-ideales-que-toda-familia-ecuatoriana-debe-conocer")
    };

    public static bool EsConsulta(string texto) => Regex.IsMatch(Normalizar(texto),
        @"\b(atractivos?|turistic[oa]s?|que visitar|que conocer|que hacer en|lugares para visitar|ruta para visitar|ruta de visita|recorrido turistico)\b");

    public static AtractivosCiudadDto? Buscar(string? ciudad)
    {
        if (string.IsNullOrWhiteSpace(ciudad)) return null;
        var nombre = Normalizar(ciudad).Replace("nazca", "nasca");
        var clave = Catalogo.Keys.FirstOrDefault(c => nombre.Contains(c, StringComparison.Ordinal));
        return clave is not null && Catalogo.TryGetValue(clave, out var guia)
            ? new(ciudad, guia.Lugares, guia.Fuente) : null;
    }

    private static string Normalizar(string valor) => new string(valor.ToLowerInvariant().Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
}
