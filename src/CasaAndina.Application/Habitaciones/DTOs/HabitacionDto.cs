namespace CasaAndina.Application.Habitaciones.DTOs;

public record HabitacionDto(
    int Id,
    string Numero,
    int Piso,
    decimal PrecioNoche,
    int Capacidad,
    string? FotoUrl,
    string SedeNombre,
    IReadOnlyList<string> Comodidades);
