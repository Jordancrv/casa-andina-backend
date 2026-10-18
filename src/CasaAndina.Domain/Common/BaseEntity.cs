namespace CasaAndina.Domain.Common;

/// <summary>
/// Propiedades comunes de persistencia. Heredar de esta clase no implica que una
/// entidad exponga operaciones de escritura en la API; Sede, por ejemplo, se
/// administra exclusivamente mediante scripts SQL.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaActualizacion { get; set; }
    public bool Activo { get; set; } = true;
}
