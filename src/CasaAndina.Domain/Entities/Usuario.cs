using CasaAndina.Domain.Common;
using CasaAndina.Domain.Enums;

namespace CasaAndina.Domain.Entities;

/// <summary>RNF01: credenciales y sesiones cifradas (hash + JWT), acceso por rol.</summary>
public class Usuario : BaseEntity
{
    public string NombreCompleto { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public RolUsuario Rol { get; set; }

    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
