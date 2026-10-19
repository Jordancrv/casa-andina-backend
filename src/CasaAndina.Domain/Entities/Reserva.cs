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
    public ICollection<DetallePago> Pagos { get; set; } = new List<DetallePago>();
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
    public DateTime? FechaCheckInReal { get; set; }
    public int? CheckInPorUsuarioId { get; set; }
    public DateTime? FechaCheckOutReal { get; set; }
    public int? CheckOutPorUsuarioId { get; set; }
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

public sealed class EstadoPago
{
    public int Id { get; set; }
    public string Codigo { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public string? Descripcion { get; set; }
    public bool EsFinal { get; set; }
    public bool Activo { get; set; }
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}

public sealed class MetodoPago
{
    public int Id { get; set; }
    public string Codigo { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public string Categoria { get; set; } = default!;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}

public sealed class Pago
{
    public int Id { get; set; }
    public int MetodoPagoId { get; set; }
    public MetodoPago MetodoPago { get; set; } = default!;
    public int EstadoPagoId { get; set; }
    public EstadoPago EstadoPago { get; set; } = default!;
    public string? ReferenciaProveedor { get; set; }
    public decimal Importe { get; set; }
    public string Moneda { get; set; } = "PEN";
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public ICollection<DetallePago> Detalles { get; set; } = new List<DetallePago>();
}

public sealed class DetallePago
{
    public int Id { get; set; }
    public int ReservaId { get; set; }
    public Reserva Reserva { get; set; } = default!;
    public int PagoId { get; set; }
    public Pago Pago { get; set; } = default!;
    public decimal MontoAplicado { get; set; }
    public DateTime FechaAplicacion { get; set; }
}
