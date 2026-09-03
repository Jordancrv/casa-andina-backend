namespace CasaAndina.Domain.Common;

/// <summary>
/// Entidad base para las tablas gestionadas por CRUD desde la API (habitaciones,
/// servicios, reservas, usuarios). Sede NO hereda de aquí: RF04 la define como
/// solo-lectura, cargada por script SQL, sin capa de escritura desde la API.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }
    public bool Activo { get; set; } = true;
}
