namespace CasaAndina.Domain.Enums;

/// <summary>RBAC (RNF01). Debe coincidir con los roles usados por el guard de rutas del frontend.</summary>
public enum RolUsuario
{
    Administrador = 1,
    Recepcion     = 2,
    Operaciones   = 3,
    Mantenimiento = 4,
    Cliente       = 5   // Portal cliente (tabla Cliente, no Usuario)
}
