using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Reservas.Queries;

public sealed record GetHabitacionImagenesQuery(int HabitacionId) : IRequest<IReadOnlyList<string>>;

public sealed class GetHabitacionImagenesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetHabitacionImagenesQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetHabitacionImagenesQuery request, CancellationToken cancellationToken)
    {
        var habitacion = await context.Habitaciones.AsNoTracking()
            .Where(h => h.Id == request.HabitacionId && h.Activo && h.Sede.Activo)
            .Select(h => new { Imagenes = h.Imagenes.OrderBy(i => i.Orden).Select(i => i.Url).ToArray() })
            .SingleOrDefaultAsync(cancellationToken);
        return habitacion?.Imagenes ?? throw new NotFoundException("Habitacion", request.HabitacionId);
    }
}
