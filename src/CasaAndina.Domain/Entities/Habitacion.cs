using CasaAndina.Domain.Common;

namespace CasaAndina.Domain.Entities;

/// <summary>RF02: inventario de habitaciones (número, piso, sede, comodidades, fotos).</summary>
public class Habitacion : BaseEntity
{
    public string Numero { get; set; } = default!;
    public int Piso { get; set; }
    public decimal PrecioNoche { get; set; }
    public int Capacidad { get; set; }
    public string? FotoUrl { get; set; }

    public int SedeId { get; set; }
    public Sede Sede { get; set; } = default!;

    public ICollection<Comodidad> Comodidades { get; set; } = new List<Comodidad>();
    public ICollection<ReservaHabitacion> Reservas { get; set; } = new List<ReservaHabitacion>();
}
