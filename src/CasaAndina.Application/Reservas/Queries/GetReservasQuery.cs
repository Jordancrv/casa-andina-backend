using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Reservas.Queries;

/// <summary>
/// RF03: lista únicamente las reservas del cliente autenticado.
/// </summary>
public record GetReservasQuery : IRequest<IReadOnlyList<ReservaDto>>;

public class GetReservasQueryHandler
    : IRequestHandler<GetReservasQuery, IReadOnlyList<ReservaDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetReservasQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ReservaDto>> Handle(
        GetReservasQuery request, CancellationToken cancellationToken)
    {
        var clienteId = _currentUser.ClienteId
            ?? throw new UnauthorizedAccessException("El token no identifica a un cliente.");

        return await _context.Reservas
            .AsNoTracking()
            .Where(r => r.ClienteId == clienteId)
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
