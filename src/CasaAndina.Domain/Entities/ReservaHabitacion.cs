namespace CasaAndina.Domain.Entities;

/// <summary>Tabla puente: una reserva puede incluir varias habitaciones.</summary>
public class ReservaHabitacion
{
    public int ReservaId { get; set; }
    public Reserva Reserva { get; set; } = default!;

    public int HabitacionId { get; set; }
    public Habitacion Habitacion { get; set; } = default!;
}
