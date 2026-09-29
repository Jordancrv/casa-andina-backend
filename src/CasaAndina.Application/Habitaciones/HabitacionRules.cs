using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;

namespace CasaAndina.Application.Habitaciones;

public static class HabitacionRules
{
    public static readonly string[] EstadosPermitidos =
        ["Disponible", "Ocupada", "Mantenimiento", "Bloqueada"];

    public static void ValidarAccesoASede(ICurrentUserService currentUser, int sedeId)
    {
        if (currentUser.Rol != RolUsuario.Administrador && !currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("El usuario no tiene una sede asignada.");

        if (currentUser.SedeId.HasValue && currentUser.SedeId.Value != sedeId)
            throw new ForbiddenAccessException("No tiene permisos para gestionar habitaciones de otra sede.");
    }
}
