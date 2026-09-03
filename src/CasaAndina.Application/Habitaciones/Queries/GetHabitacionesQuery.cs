using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common.Models;
using CasaAndina.Application.Habitaciones.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Habitaciones.Queries;

/// <summary>RF02: búsqueda con filtros por sede y piso, resultado paginado.</summary>
public record GetHabitacionesQuery(
    int? SedeId,
    int? Piso,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PaginatedList<HabitacionDto>>;

public class GetHabitacionesQueryHandler
    : IRequestHandler<GetHabitacionesQuery, PaginatedList<HabitacionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetHabitacionesQueryHandler(IApplicationDbContext context) => _context = context;

    public Task<PaginatedList<HabitacionDto>> Handle(
        GetHabitacionesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Habitaciones
            .Where(h => h.Activo)
            .Where(h => request.SedeId == null || h.SedeId == request.SedeId)
            .Where(h => request.Piso == null || h.Piso == request.Piso)
            .OrderBy(h => h.SedeId).ThenBy(h => h.Numero)
            .Select(h => new HabitacionDto(
                h.Id, h.Numero, h.Piso, h.PrecioNoche, h.Capacidad, h.FotoUrl,
                h.Sede.Nombre,
                h.Comodidades.Select(c => c.Nombre).ToList()));

        return PaginatedList<HabitacionDto>.CreateAsync(
            query, request.PageNumber, request.PageSize, cancellationToken);
    }
}
