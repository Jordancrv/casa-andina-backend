using CasaAndina.Domain.Common;

namespace CasaAndina.Domain.Entities;

/// <summary>
/// Cabecera de compra; las habitaciones, fechas y huéspedes residen en sus detalles.
/// </summary>
public class Reserva : BaseEntity
{
    public string CodigoReserva { get; set; } = default!;   // → columna CodigoReserva (CA-XXXXX)

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = default!;

    public int SedeId { get; set; }
    public Sede Sede { get; set; } = default!;

    public int? CreadoPorUsuarioId { get; set; }            // NULL si el cliente lo creó solo
    public Usuario? CreadoPorUsuario { get; set; }

    public string Estado { get; set; } = "Pendiente";       // Pendiente/Confirmada/Bloqueada/Completada/Cancelada
    public string Canal { get; set; } = "Directo";          // Directo / OTA
    public decimal PrecioTotal { get; set; }                // → columna PrecioTotal en BD
    public Comprobante? Comprobante { get; set; }
    public ICollection<ReservaHabitacion> Habitaciones { get; set; } = new List<ReservaHabitacion>();
    public ICollection<ReservaServicio> Servicios { get; set; } = new List<ReservaServicio>();
}

public class ReservaHabitacion
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public Reserva Reserva { get; set; } = default!;
    public int HabitacionId { get; set; }
    public Habitacion Habitacion { get; set; } = default!;
    public DateTime FechaCheckIn { get; set; }
    public DateTime FechaCheckOut { get; set; }
    public int CantidadPersonas { get; set; }
    public decimal PrecioAcordado { get; set; }
    public decimal Subtotal { get; set; }
    public ICollection<AsignacionHuesped> Huespedes { get; set; } = new List<AsignacionHuesped>();
}

public class ReservaServicio
{
    public int ReservaId { get; set; }
    public Reserva Reserva { get; set; } = default!;
    public int ServicioId { get; set; }
    public Servicio Servicio { get; set; } = default!;
    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

public sealed class Comprobante
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public Reserva Reserva { get; set; } = default!;
    public string NumeroComprobante { get; set; } = default!;
    public CasaAndina.Domain.Enums.TipoComprobante Tipo { get; set; } = CasaAndina.Domain.Enums.TipoComprobante.Boleta;
    public DateTime FechaEmision { get; set; }
    public decimal MontoTotal { get; set; }
    public string? UrlPdf { get; set; }
}
