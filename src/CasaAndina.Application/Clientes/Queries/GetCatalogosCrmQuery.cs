using CasaAndina.Application.Clientes.DTOs;
using CasaAndina.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Clientes.Queries;

public sealed record GetCatalogosCrmQuery : IRequest<CatalogosCrmDto>;
public sealed class GetCatalogosCrmQueryHandler(IApplicationDbContext context, ICurrentUserService user)
    : IRequestHandler<GetCatalogosCrmQuery, CatalogosCrmDto>
{
    public async Task<CatalogosCrmDto> Handle(GetCatalogosCrmQuery request, CancellationToken ct)
    {
        var sede = ClientesScope.Sede(user);
        var sedes = await context.Sedes.AsNoTracking().Where(s => s.Activo && (!sede.HasValue || s.Id == sede))
            .OrderBy(s => s.Nombre).Select(s => new SedeCrmDto(s.Id, s.Nombre, s.Ciudad)).ToListAsync(ct);
        var tipos = await context.TiposDocumento.AsNoTracking().Where(d => d.Activo).OrderBy(d => d.Id)
            .Select(d => new DocumentoCrmDto(d.Id, d.Codigo, d.Nombre, d.TipoPersonaPermitida, d.LongitudMinima, d.LongitudMaxima)).ToListAsync(ct);
        return new(sedes, tipos, !sede.HasValue, sede);
    }
}
