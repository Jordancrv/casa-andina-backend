namespace CasaAndina.Domain.Entities;

/// <summary>
/// Comodidades de habitación (WiFi, AC, TV/Cable, Jacuzzi).
/// BD: ComodidadId, Nombre. NO tiene FechaCreacion, Activo, FechaActualizacion, Icono.
/// </summary>
public class Comodidad
{
    public int Id { get; set; }        // → ComodidadId en BD
    public string Nombre { get; set; } = default!;

    public ICollection<Habitacion> Habitaciones { get; set; } = new List<Habitacion>();
}
