using CasaAndina.Domain.Common;

namespace CasaAndina.Domain.Entities;

public class Comodidad : BaseEntity
{
    public string Nombre { get; set; } = default!;
    public string? Icono { get; set; }

    public ICollection<Habitacion> Habitaciones { get; set; } = new List<Habitacion>();
}
