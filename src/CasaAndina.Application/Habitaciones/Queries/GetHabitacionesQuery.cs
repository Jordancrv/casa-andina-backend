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

    public async Task<PaginatedList<HabitacionDto>> Handle(
        GetHabitacionesQuery request, CancellationToken cancellationToken)
    {
        var baseQuery = _context.Habitaciones
            .AsNoTracking()
            .Include(h => h.Sede)
            .Include(h => h.TipoHabitacion)
            .Include(h => h.Comodidades)
            .Where(h => h.Activo)
            .Where(h => request.SedeId == null || h.SedeId == request.SedeId)
            .Where(h => request.Piso == null || h.Piso == request.Piso)
            .OrderBy(h => h.SedeId).ThenBy(h => h.Numero);

        var count = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(h => new HabitacionDto(
                h.Id,
                h.Numero,
                h.Piso,
                h.PrecioNoche,
                h.Estado,
                h.FotoUrl,
                h.Sede != null ? h.Sede.Nombre : string.Empty,
                h.TipoHabitacion != null ? h.TipoHabitacion.Nombre : string.Empty,
                h.TipoHabitacion != null ? h.TipoHabitacion.CapacidadAdultos : (byte)2,
                h.TipoHabitacion != null ? h.TipoHabitacion.CapacidadNinos : (byte)0,
                h.Comodidades.Select(c => c.Nombre).ToList()))
            .ToListAsync(cancellationToken);

        return new PaginatedList<HabitacionDto>(items, count, request.PageNumber, request.PageSize);
    }
}
