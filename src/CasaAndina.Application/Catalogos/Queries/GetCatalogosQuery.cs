using CasaAndina.Application.Catalogos.DTOs;
using CasaAndina.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Catalogos.Queries;

public record GetCatalogosQuery : IRequest<CatalogosResponse>;

public class GetCatalogosQueryHandler : IRequestHandler<GetCatalogosQuery, CatalogosResponse>
{
    private readonly IApplicationDbContext _context;

    public GetCatalogosQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<CatalogosResponse> Handle(GetCatalogosQuery request, CancellationToken cancellationToken)
    {
        var tipos = await _context.TiposHabitacion
            .Select(t => new TipoHabitacionDto(t.Id, t.Nombre, t.Descripcion, t.CapacidadAdultos, t.CapacidadNinos))
            .ToListAsync(cancellationToken);

        var comodidades = await _context.Comodidades
            .Select(c => new ComodidadDto(c.Id, c.Nombre))
            .ToListAsync(cancellationToken);

        var sedes = await _context.Sedes
            .Where(s => s.Activo)
            .Select(s => new SedeDto(s.Id, s.Nombre, s.Ciudad))
            .ToListAsync(cancellationToken);

        return new CatalogosResponse(tipos, comodidades, sedes);
    }
}
