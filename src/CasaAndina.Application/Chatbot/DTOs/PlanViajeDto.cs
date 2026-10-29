namespace CasaAndina.Application.Chatbot.DTOs;

public sealed record PlanViajeDto(
    string Resumen,
    bool UsaIa,
    DateTimeOffset GeneradoEn,
    IReadOnlyList<EstadiaRecomendadaDto> Recomendaciones,
    IReadOnlyList<string> Avisos);

public sealed record EstadiaRecomendadaDto(
    int SedeId, string SedeNombre, string Ciudad, string Categoria,
    decimal PrecioPorNoche, decimal TotalEstadia, int Noches,
    string TipoHabitacion, string Motivo, ClimaViajeDto Clima,
    int? HabitacionId = null, string? FotoUrl = null, int ReservasRecientes = 0);

public sealed record MensajeViajeDto(string Rol, string Texto);

public sealed record ClimaViajeDto(
    string Estado, string Aviso, string? FuenteUrl,
    DateTimeOffset? ConsultadoEn, IReadOnlyList<DiaClimaDto> Dias);

public sealed record DiaClimaDto(
    string Fecha, double MinimaC, double MaximaC, double? ProbabilidadLluvia, int? CodigoTiempo = null);
