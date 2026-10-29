namespace CasaAndina.Domain.Entities;
public sealed class Permiso { public string Codigo { get; set; } = ""; public string Seccion { get; set; } = ""; public bool Delegable { get; set; } }
public sealed class RolPermiso { public int RolId { get; set; } public Rol Rol { get; set; } = default!; public string PermisoCodigo { get; set; } = ""; public Permiso Permiso { get; set; } = default!; }
