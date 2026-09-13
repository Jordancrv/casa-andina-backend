using CasaAndina.Domain.Common;

namespace CasaAndina.Domain.Entities;

/// <summary>
/// RF04: inventario de habitaciones por sede.
/// Columnas BD: HabitacionId, SedeId, TipoHabitacionId, Numero, Piso,
///              PrecioBase, Estado, FotoUrl, Activo, FechaCreacion
/// </summary>
public class Habitacion : BaseEntity
{
    public int SedeId { get; set; }
    public Sede Sede { get; set; } = default!;

    public int TipoHabitacionId { get; set; }
    public TipoHabitacion TipoHabitacion { get; set; } = default!;

    public string Numero { get; set; } = default!;
    public int Piso { get; set; }
    public decimal PrecioNoche { get; set; }   // → columna PrecioBase en BD
    public string Estado { get; set; } = "Disponible";   // Disponible / Ocupada / Mantenimiento / Bloqueada
    public string? FotoUrl { get; set; }

    public ICollection<Comodidad> Comodidades { get; set; } = new List<Comodidad>();
}
