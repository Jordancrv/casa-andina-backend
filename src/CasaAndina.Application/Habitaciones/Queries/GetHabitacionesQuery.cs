using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common.Models;
using CasaAndina.Application.Habitaciones.DTOs;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Habitaciones.Queries;

/// <summary>RF02: búsqueda con filtros por sede y piso, resultado paginado.</summary>
public record GetHabitacionesQuery(
    int? SedeId,
    int? Piso,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PaginatedList<HabitacionDto>>;

public class GetHabitacionesQueryValidator : AbstractValidator<GetHabitacionesQuery>
{
    public GetHabitacionesQueryValidator()
    {
        RuleFor(x => x.SedeId).GreaterThan(0).When(x => x.SedeId.HasValue);
        RuleFor(x => x.Piso).GreaterThanOrEqualTo(0).When(x => x.Piso.HasValue);
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class GetHabitacionesQueryHandler
    : IRequestHandler<GetHabitacionesQuery, PaginatedList<HabitacionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetHabitacionesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedList<HabitacionDto>> Handle(
        GetHabitacionesQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.Rol != RolUsuario.Administrador && !_currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("El usuario no tiene una sede asignada.");

        if (request.SedeId.HasValue)
            HabitacionRules.ValidarAccesoASede(_currentUser, request.SedeId.Value);

        var sedeId = _currentUser.SedeId ?? request.SedeId;
        var baseQuery = _context.Habitaciones
            .AsNoTracking()
            .Include(h => h.Sede)
            .Include(h => h.TipoHabitacion)
            .Include(h => h.Comodidades)
            .Where(h => h.Activo)
            .Where(h => sedeId == null || h.SedeId == sedeId)
            .Where(h => request.Piso == null || h.Piso == request.Piso)
            .OrderBy(h => h.SedeId).ThenBy(h => h.Numero);

        var count = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(h => new HabitacionDto(
                h.Id,
                h.SedeId,
                h.TipoHabitacionId,
                h.Numero,
                h.Piso,
                h.PrecioNoche,
                h.Estado,
                h.FotoUrl,
                h.Sede != null ? h.Sede.Nombre : string.Empty,
                h.TipoHabitacion != null ? h.TipoHabitacion.Nombre : string.Empty,
                h.TipoHabitacion != null ? h.TipoHabitacion.CapacidadAdultos : (byte)2,
                h.TipoHabitacion != null ? h.TipoHabitacion.CapacidadNinos : (byte)0,
                h.Comodidades.Select(c => c.Id).ToList(),
                h.Comodidades.Select(c => c.Nombre).ToList(),
                h.Activo))
            .ToListAsync(cancellationToken);

        return new PaginatedList<HabitacionDto>(items, count, request.PageNumber, request.PageSize);
    }
}
