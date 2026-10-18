namespace CasaAndina.Domain.Enums;

public static class RolCodigo
{
    public static RolUsuario? Mapear(string codigo) => codigo switch
    {
        "SUPERADMINISTRADOR" => RolUsuario.Superadministrador,
        "ADMINISTRADOR" => RolUsuario.Administrador,
        "RECEPCIONISTA" or "RECEPCION" => RolUsuario.Recepcion,
        "CLIENTE" => RolUsuario.Cliente,
        "OPERACIONES" => RolUsuario.Operaciones,
        "MANTENIMIENTO" => RolUsuario.Mantenimiento,
        _ => null
    };

    public static int Prioridad(RolUsuario rol) => rol switch
    {
        RolUsuario.Superadministrador => 0,
        RolUsuario.Administrador => 1,
        RolUsuario.Recepcion => 2,
        RolUsuario.Operaciones => 3,
        RolUsuario.Mantenimiento => 4,
        RolUsuario.Cliente => 5,
        _ => int.MaxValue
    };
}
