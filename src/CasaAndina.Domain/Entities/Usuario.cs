using CasaAndina.Domain.Common;
using CasaAndina.Domain.Enums;

namespace CasaAndina.Domain.Entities;

/// <summary>RNF01: credenciales y sesiones cifradas (hash + JWT), acceso por rol.</summary>
public class Usuario : BaseEntity
{
    public string NombreCompleto { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;

    /// <summary>
    /// FK a la tabla Rol. El nombre del rol se lee via la propiedad de navegación RolEntidad.
    /// Se mapea al enum RolUsuario en la configuración.
    /// </summary>
    public int RolId { get; set; }

    /// <summary>Derivado del Nombre en la tabla Rol — NO una columna directa en Usuario.</summary>
    public RolUsuario Rol => RolId switch
    {
        1 => RolUsuario.Administrador,
        2 => RolUsuario.Recepcion,
        3 => RolUsuario.Operaciones,
        4 => RolUsuario.Mantenimiento,
        _ => RolUsuario.Administrador
    };

    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
