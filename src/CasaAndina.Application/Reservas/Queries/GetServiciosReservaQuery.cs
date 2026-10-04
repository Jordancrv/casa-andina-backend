using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Servicios.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Reservas.Queries;

public sealed record GetServiciosReservaQuery(int SedeId) : IRequest<IReadOnlyList<ServicioDto>>;

public sealed class GetServiciosReservaQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetServiciosReservaQuery, IReadOnlyList<ServicioDto>>
{
    public async Task<IReadOnlyList<ServicioDto>> Handle(GetServiciosReservaQuery request, CancellationToken cancellationToken)
    {
        if (request.SedeId <= 0) throw new ArgumentException("Selecciona una sede válida.");
        return await context.Servicios.AsNoTracking().Where(x => x.Activo && x.Sedes.Any(s => s.Id == request.SedeId))
            .OrderBy(x => x.Nombre)
            .Select(x => new ServicioDto(x.Id, x.Nombre, x.Descripcion, x.Precio,
                x.Sedes.Select(s => s.Id).ToList(), x.Sedes.Select(s => s.Nombre).ToList(), x.Activo))
            .ToListAsync(cancellationToken);
    }
}
