using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Reservas.Queries;

/// <summary>
/// RF03: lista las reservas filtradas opcionalmente por cliente, ordenadas por fecha check-in.
/// </summary>
public record GetReservasQuery(int? ClienteId = null) : IRequest<IReadOnlyList<ReservaDto>>;

public class GetReservasQueryHandler
    : IRequestHandler<GetReservasQuery, IReadOnlyList<ReservaDto>>
{
    private readonly IApplicationDbContext _context;

    public GetReservasQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ReservaDto>> Handle(
        GetReservasQuery request, CancellationToken cancellationToken)
    {
        return await _context.Reservas
            .Where(r => request.ClienteId == null || r.ClienteId == request.ClienteId)
            .OrderByDescending(r => r.FechaCheckIn)
            .Select(r => new ReservaDto(
                r.Id,
                r.CodigoReserva,
                r.FechaCheckIn,
                r.FechaCheckOut,
                r.PrecioTotal,
                r.Estado,
                r.Habitacion.Sede.Nombre,
                r.Habitacion.Sede.Ciudad,
                new[] { r.Habitacion.Numero }))
            .ToListAsync(cancellationToken);
    }
}
