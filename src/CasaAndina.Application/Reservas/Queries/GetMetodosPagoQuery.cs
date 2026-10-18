using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Reservas.Queries;

public sealed record GetMetodosPagoQuery : IRequest<IReadOnlyList<MetodoPagoDto>>;

public sealed class GetMetodosPagoQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMetodosPagoQuery, IReadOnlyList<MetodoPagoDto>>
{
    public async Task<IReadOnlyList<MetodoPagoDto>> Handle(
        GetMetodosPagoQuery request, CancellationToken cancellationToken)
        => await context.MetodosPago.AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Nombre)
            .Select(x => new MetodoPagoDto(x.Codigo, x.Nombre, x.Categoria))
            .ToListAsync(cancellationToken);
}
