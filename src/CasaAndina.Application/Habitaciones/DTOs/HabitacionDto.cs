namespace CasaAndina.Application.Habitaciones.DTOs;

public record HabitacionDto(
    int Id,
    string Numero,
    int Piso,
    decimal PrecioNoche,
    string Estado,
    string? FotoUrl,
    string SedeNombre,
    string TipoHabitacionNombre,
    byte CapacidadAdultos,
    byte CapacidadNinos,
    IReadOnlyList<string> Comodidades);
