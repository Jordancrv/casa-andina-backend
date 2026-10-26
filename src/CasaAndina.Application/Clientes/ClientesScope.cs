using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;

namespace CasaAndina.Application.Clientes;

public static class ClientesScope
{
    public static int? Sede(ICurrentUserService user, int? solicitada = null)
    {
        if (user.Rol is not (RolUsuario.Administrador or RolUsuario.Superadministrador))
            throw new ForbiddenAccessException("No tiene permisos para gestionar clientes.");
        if (user.Rol != RolUsuario.Superadministrador && !user.SedeId.HasValue)
            throw new ForbiddenAccessException("La cuenta administrativa no tiene una sede asignada.");
        if (user.SedeId.HasValue && solicitada.HasValue && user.SedeId != solicitada)
            throw new ForbiddenAccessException("No tiene acceso a esa sede.");
        return user.SedeId ?? solicitada;
    }

    public static IQueryable<Cliente> Aplicar(IQueryable<Cliente> clientes, int? sedeId)
        => sedeId.HasValue ? clientes.Where(c => c.SedeRegistroId == sedeId || c.Reservas.Any(r => r.SedeId == sedeId)) : clientes;
}
