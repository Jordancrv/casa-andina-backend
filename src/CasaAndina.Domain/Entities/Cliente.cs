namespace CasaAndina.Domain.Entities;

/// <summary>
/// Huéspedes / clientes del Portal Cliente.
/// Columnas BD: ClienteId, NombreCompleto, DNI, Correo, Telefono,
///              ContrasenaHash, FotoUrl, Estado, FechaRegistro
/// </summary>
public class Cliente
{
    public int Id { get; set; }   // → ClienteId en BD
    public string NombreCompleto { get; set; } = default!;
    public string DNI { get; set; } = default!;
    public string Correo { get; set; } = default!;
    public string? Telefono { get; set; }
    public string? ContrasenaHash { get; set; }
    public string? FotoUrl { get; set; }
    public string Estado { get; set; } = "Activo";
    public DateTime FechaRegistro { get; set; }

    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
