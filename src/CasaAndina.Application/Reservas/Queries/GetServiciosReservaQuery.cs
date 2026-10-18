using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Reservas.Queries;

public sealed record GetServiciosReservaQuery(int SedeId) : IRequest<IReadOnlyList<ServicioDisponibleDto>>;

public sealed class GetServiciosReservaQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetServiciosReservaQuery, IReadOnlyList<ServicioDisponibleDto>>
{
    public async Task<IReadOnlyList<ServicioDisponibleDto>> Handle(GetServiciosReservaQuery request, CancellationToken cancellationToken)
    {
        if (request.SedeId <= 0) throw new ArgumentException("Selecciona una sede válida.");
        return await context.Servicios.AsNoTracking().Where(x => x.Activo && x.Sedes.Any(s => s.Id == request.SedeId))
            .OrderBy(x => x.Nombre)
            .Select(x => new ServicioDisponibleDto(x.Id, x.Nombre, x.Descripcion, x.Precio))
            .ToListAsync(cancellationToken);
    }
}
