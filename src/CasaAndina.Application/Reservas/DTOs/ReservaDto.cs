namespace CasaAndina.Application.Reservas.DTOs;

/// <summary>
/// Proyección plana de una Reserva para el Portal Cliente (RF03: "Mis Reservas").
/// </summary>
public record ReservaDto(
    int ReservaId,
    string Codigo,
    DateTime FechaCheckIn,
    DateTime FechaCheckOut,
    decimal MontoTotal,
    string Estado,
    string SedeNombre,
    string Ciudad,
    IReadOnlyList<string> HabitacionesNumeros);
