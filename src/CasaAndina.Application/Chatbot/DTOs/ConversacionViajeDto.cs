namespace CasaAndina.Application.Chatbot.DTOs;

public sealed record PreferenciasConversacionDto(
    DateTime? FechaEntrada = null, DateTime? FechaSalida = null,
    decimal? PresupuestoPorNoche = null, int? Adultos = null,
    string? Ciudad = null, bool DestinoFlexible = false);

public sealed record InterpretacionViajeDto(string Mensaje, PreferenciasConversacionDto Preferencias, bool BuscarOpciones);
public sealed record ConversacionViajeDto(string Mensaje, bool UsaIa, PreferenciasConversacionDto Preferencias,
    PlanViajeDto? Plan, IReadOnlyList<string> Sugerencias);
