namespace CasaAndina.Domain.Enums;

/// <summary>RBAC (RNF01). Debe coincidir con los roles usados por el guard de rutas del frontend.</summary>
public enum RolUsuario
{
    Superadministrador = 1,
    Administrador = 2,
    Recepcion = 3,
    Cliente = 4,
    Operaciones = 5,
    Mantenimiento = 6
}
