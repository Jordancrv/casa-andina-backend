using CasaAndina.Domain.Common;

namespace CasaAndina.Domain.Entities;

/// <summary>RNF01: credenciales y sesiones cifradas (hash + JWT), acceso por rol.</summary>
public class Usuario : BaseEntity
{
    public string Correo { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public DateTime? UltimoAcceso { get; set; }

    public int? SedeId { get; set; }
    public Sede? Sede { get; set; }

    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = default!;

    public ICollection<UsuarioRol> Roles { get; set; } = new List<UsuarioRol>();

    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
