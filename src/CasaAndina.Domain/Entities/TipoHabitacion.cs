namespace CasaAndina.Domain.Entities;

/// <summary>
/// Tabla dbo.TipoHabitacion (Superior King, Superior Twin, Andina Suite, Matrimonial)
/// </summary>
public class TipoHabitacion
{
    public int Id { get; set; } // → TipoHabitacionId
    public string Nombre { get; set; } = default!;
    public string? Descripcion { get; set; }
    public byte CapacidadAdultos { get; set; } = 2;
    public byte CapacidadNinos { get; set; } = 0;
}
