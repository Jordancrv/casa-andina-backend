using CasaAndina.Domain.Common;
using CasaAndina.Domain.Enums;

namespace CasaAndina.Domain.Entities;

/// <summary>RNF01: credenciales y sesiones cifradas (hash + JWT), acceso por rol.</summary>
public class Usuario : BaseEntity
{
    public string NombreCompleto { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string? Telefono { get; set; }
    public DateTime? UltimoAcceso { get; set; }

    public int? SedeId { get; set; }
    public Sede? Sede { get; set; }

    /// <summary>
    /// FK a la tabla Rol. El código persistido se traduce al identificador estable
    /// que utilizan JWT y frontend.
    /// </summary>
    public int RolId { get; set; }
    public Rol RolAsignado { get; set; } = default!;

    /// <summary>Derivado de Rol.Codigo; no depende del id ni del nombre visible.</summary>
    public RolUsuario Rol => RolAsignado.Codigo switch
    {
        "ADMINISTRADOR" => RolUsuario.Administrador,
        "RECEPCION" => RolUsuario.Recepcion,
        "OPERACIONES" => RolUsuario.Operaciones,
        "MANTENIMIENTO" => RolUsuario.Mantenimiento,
        _ => throw new InvalidOperationException($"El código de rol '{RolAsignado.Codigo}' no está soportado.")
    };

    public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
}
