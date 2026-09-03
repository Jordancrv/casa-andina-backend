using CasaAndina.Domain.Common;

namespace CasaAndina.Domain.Entities;

/// <summary>
/// RF04: entidad de solo lectura desde la API. Se puebla exclusivamente mediante
/// scripts SQL (ver Infrastructure/Persistence/Migrations). No exponer POST/PUT/DELETE
/// para Sede en ningún controller — es una restricción de alcance explícita del acta.
/// </summary>
public class Sede : BaseEntity
{
    public string Nombre { get; set; } = default!;
    public string Ciudad { get; set; } = default!;
    public string Direccion { get; set; } = default!;

    public ICollection<Habitacion> Habitaciones { get; set; } = new List<Habitacion>();
    public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
}
