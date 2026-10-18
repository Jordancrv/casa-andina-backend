namespace CasaAndina.Domain.Entities;

/// <summary>
/// Perfil comercial de una persona que compra o figura como titular.
/// </summary>
public class Cliente
{
    public int Id { get; set; }   // → ClienteId en BD
    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = default!;

    public string Estado { get; set; } = "Activo";
    public DateTime FechaRegistro { get; set; }

    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
