using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Habitaciones.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Habitaciones.Queries;

public record GetHabitacionByIdQuery(int Id) : IRequest<HabitacionDto>;

public class GetHabitacionByIdQueryHandler : IRequestHandler<GetHabitacionByIdQuery, HabitacionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetHabitacionByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<HabitacionDto> Handle(GetHabitacionByIdQuery request, CancellationToken cancellationToken)
    {
        var habitacion = await _context.Habitaciones.AsNoTracking()
            .Where(h => h.Id == request.Id && h.Activo)
            .Select(h => new HabitacionDto(
                h.Id, h.SedeId, h.TipoHabitacionId, h.Numero, h.Piso, h.PrecioNoche,
                h.Estado, h.FotoUrl, h.Sede.Nombre, h.TipoHabitacion.Nombre,
                h.TipoHabitacion.CapacidadAdultos, h.TipoHabitacion.CapacidadNinos,
                h.Comodidades.Select(c => c.Id).ToList(),
                h.Comodidades.Select(c => c.Nombre).ToList(), h.Activo))
            .SingleOrDefaultAsync(cancellationToken);

        if (habitacion is null)
            throw new NotFoundException("Habitacion", request.Id);

        HabitacionRules.ValidarAccesoASede(_currentUser, habitacion.SedeId);
        return habitacion;
    }
}
