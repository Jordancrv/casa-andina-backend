namespace CasaAndina.Application.Dashboard.DTOs;

/// <summary>RF01: widgets base del Dashboard BI. Ampliar según lo que defina el equipo de BI.</summary>
public record DashboardResumenDto(
    int HabitacionesOcupadas,
    int HabitacionesDisponibles,
    int ReservasActivas,
    decimal IngresosMesActual);
