namespace CasaAndina.Application.Habitaciones.DTOs;

public record HabitacionDto(
    int Id,
    int SedeId,
    int TipoHabitacionId,
    string Numero,
    int Piso,
    decimal PrecioNoche,
    string Estado,
    string? FotoUrl,
    string SedeNombre,
    string TipoHabitacionNombre,
    byte CapacidadAdultos,
    byte CapacidadNinos,
    IReadOnlyList<int> ComodidadesIds,
    IReadOnlyList<string> Comodidades,
    bool Activo);
