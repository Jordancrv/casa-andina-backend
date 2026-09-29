using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common.Models;
using CasaAndina.Application.Servicios.DTOs;
using CasaAndina.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Servicios.Queries;

public record GetServiciosQuery(
    int? SedeId,
    string? Nombre,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PaginatedList<ServicioDto>>;

public class GetServiciosQueryValidator : AbstractValidator<GetServiciosQuery>
{
    public GetServiciosQueryValidator()
    {
        RuleFor(x => x.SedeId).GreaterThan(0).When(x => x.SedeId.HasValue);
        RuleFor(x => x.Nombre).MaximumLength(100);
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class GetServiciosQueryHandler : IRequestHandler<GetServiciosQuery, PaginatedList<ServicioDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetServiciosQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedList<ServicioDto>> Handle(
        GetServiciosQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.Rol != RolUsuario.Administrador && !_currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("El usuario no tiene una sede asignada.");
        if (_currentUser.SedeId.HasValue && request.SedeId.HasValue &&
            _currentUser.SedeId.Value != request.SedeId.Value)
            throw new ForbiddenAccessException("No tiene permisos para consultar servicios de otra sede.");

        var sedeId = _currentUser.SedeId ?? request.SedeId;
        var nombre = request.Nombre?.Trim();
        var query = _context.Servicios.AsNoTracking()
            .Where(s => s.Activo)
            .Where(s => !sedeId.HasValue || s.Sedes.Any(se => se.Id == sedeId.Value))
            .Where(s => string.IsNullOrEmpty(nombre) || s.Nombre.Contains(nombre))
            .OrderBy(s => s.Nombre)
            .Select(s => new ServicioDto(
                s.Id, s.Nombre, s.Descripcion, s.Precio,
                s.Sedes.Select(se => se.Id).ToList(),
                s.Sedes.Select(se => se.Nombre).ToList(),
                s.Activo));

        return await PaginatedList<ServicioDto>.CreateAsync(
            query, request.PageNumber, request.PageSize, cancellationToken);
    }
}
