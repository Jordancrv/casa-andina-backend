using CasaAndina.Domain.Common;
using CasaAndina.Domain.Enums;

namespace CasaAndina.Domain.Entities;

/// <summary>RF07: código único (CA-XXXXX), comprobante digital, filtro activa/histórica.</summary>
public class Reserva : BaseEntity
{
    public string Codigo { get; set; } = default!; // formato CA-XXXXX
    public DateTime FechaCheckIn { get; set; }
    public DateTime FechaCheckOut { get; set; }
    public decimal MontoTotal { get; set; }
    public EstadoReserva Estado { get; set; } = EstadoReserva.Activa;

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = default!;

    public ICollection<ReservaHabitacion> Habitaciones { get; set; } = new List<ReservaHabitacion>();
}
