namespace CasaAndina.Domain.Entities;

/// <summary>Rol persistido para el personal interno del portal administrativo.</summary>
public sealed class Rol
{
    public int Id { get; set; }
    public string Codigo { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public string? Descripcion { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
