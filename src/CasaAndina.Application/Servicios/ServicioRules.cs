using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;

namespace CasaAndina.Application.Servicios;

public static class ServicioRules
{
    public static IReadOnlyList<int> ValidarSedes(
        ICurrentUserService currentUser, IEnumerable<int> sedesIds)
    {
        var ids = sedesIds.Distinct().ToList();
        if (ids.Count == 0)
            throw new ForbiddenAccessException("El servicio debe asignarse al menos a una sede.");

        if (currentUser.Rol == RolUsuario.Administrador)
            return ids;

        if (!currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("El usuario no tiene una sede asignada.");

        if (ids.Count != 1 || ids[0] != currentUser.SedeId.Value)
            throw new ForbiddenAccessException("Solo puede gestionar servicios de su sede.");

        return ids;
    }

    public static void ValidarServicioAsignado(
        ICurrentUserService currentUser, IReadOnlyCollection<int> sedesIds)
    {
        if (currentUser.Rol == RolUsuario.Administrador)
            return;

        if (!currentUser.SedeId.HasValue ||
            sedesIds.Count != 1 ||
            !sedesIds.Contains(currentUser.SedeId.Value))
            throw new ForbiddenAccessException("No tiene permisos para modificar este servicio.");
    }
}
