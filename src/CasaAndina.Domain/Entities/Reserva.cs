using CasaAndina.Domain.Common;

namespace CasaAndina.Domain.Entities;

/// <summary>
/// RF07: reserva de habitación. Columna BD: ReservaId, CodigoReserva,
/// ClienteId, HabitacionId, CreadoPorUsuarioId, FechaCheckIn,
/// FechaCheckOut, NumAdultos, NumNinos, Estado, Canal, PrecioTotal, FechaCreacion.
/// La BD vincula Reserva→Habitacion directamente (1:1), no hay tabla puente.
/// </summary>
public class Reserva : BaseEntity
{
    public string CodigoReserva { get; set; } = default!;   // → columna CodigoReserva (CA-XXXXX)

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = default!;

    public int HabitacionId { get; set; }
    public Habitacion Habitacion { get; set; } = default!;

    public int? CreadoPorUsuarioId { get; set; }            // NULL si el cliente lo creó solo
    public Usuario? CreadoPorUsuario { get; set; }

    public DateTime FechaCheckIn { get; set; }
    public DateTime FechaCheckOut { get; set; }
    public int NumAdultos { get; set; } = 1;
    public int NumNinos { get; set; } = 0;

    public string Estado { get; set; } = "Pendiente";       // Pendiente/Confirmada/Bloqueada/Completada/Cancelada
    public string Canal { get; set; } = "Directo";          // Directo / OTA
    public decimal PrecioTotal { get; set; }                // → columna PrecioTotal en BD
}
