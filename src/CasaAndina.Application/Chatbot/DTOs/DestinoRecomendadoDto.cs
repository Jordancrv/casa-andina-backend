namespace CasaAndina.Application.Chatbot.DTOs;

/// <summary>
/// RF05/RF06: representa una sede recomendada por el motor de recomendación
/// basado en el presupuesto y el clima preferido del cliente.
/// </summary>
public record DestinoRecomendadoDto(
    int SedeId,
    string SedeNombre,
    string Ciudad,
    string Region,
    string Categoria,
    decimal PrecioBaseDesde,
    string? Descripcion);
