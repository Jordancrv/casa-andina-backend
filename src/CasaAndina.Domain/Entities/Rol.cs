namespace CasaAndina.Domain.Entities;

/// <summary>Rol asignable a una cuenta de usuario.</summary>
public sealed class Rol
{
    public int Id { get; set; }
    public string Codigo { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public string? Descripcion { get; set; }

    public ICollection<UsuarioRol> Usuarios { get; set; } = new List<UsuarioRol>();
}

public class UsuarioRol
{
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = default!;
    public int RolId { get; set; }
    public Rol Rol { get; set; } = default!;
    public DateTime FechaAsignacion { get; set; }
}
