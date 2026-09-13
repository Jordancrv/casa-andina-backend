using CasaAndina.Domain.Common;

namespace CasaAndina.Domain.Entities;

/// <summary>
/// RF04: sedes físicas de Casa Andina. Solo lectura desde la API (RF04).
/// Columnas BD: SedeId, Nombre, Codigo, Region, Ciudad, Direccion,
///              Categoria, Telefono, Activo, FechaCreacion
/// </summary>
public class Sede : BaseEntity
{
    public string Nombre { get; set; } = default!;
    public string Codigo { get; set; } = default!;          // CUS, MIR, ARQ...
    public string Region { get; set; } = default!;          // Costa / Sierra / Selva
    public string Ciudad { get; set; } = default!;
    public string? Direccion { get; set; }
    public string Categoria { get; set; } = default!;       // Premium / Select / Standard
    public string? Telefono { get; set; }

    public ICollection<Habitacion> Habitaciones { get; set; } = new List<Habitacion>();
    public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
}
