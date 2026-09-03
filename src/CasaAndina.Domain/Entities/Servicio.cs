using CasaAndina.Domain.Common;

namespace CasaAndina.Domain.Entities;

/// <summary>RF03: servicios complementarios, asignables a múltiples sedes.</summary>
public class Servicio : BaseEntity
{
    public string Nombre { get; set; } = default!;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }

    public ICollection<Sede> Sedes { get; set; } = new List<Sede>();
}
