using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Servicios.DTOs;
using CasaAndina.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Servicios.Queries;

public record GetServicioByIdQuery(int Id) : IRequest<ServicioDto>;

public class GetServicioByIdQueryHandler : IRequestHandler<GetServicioByIdQuery, ServicioDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetServicioByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ServicioDto> Handle(GetServicioByIdQuery request, CancellationToken cancellationToken)
    {
        var servicio = await _context.Servicios.AsNoTracking()
            .Where(s => s.Id == request.Id && s.Activo)
            .Select(s => new ServicioDto(
                s.Id, s.Nombre, s.Descripcion, s.Precio,
                s.Sedes.Select(se => se.Id).ToList(),
                s.Sedes.Select(se => se.Nombre).ToList(),
                s.Activo))
            .SingleOrDefaultAsync(cancellationToken);

        if (servicio is null)
            throw new NotFoundException("Servicio", request.Id);

        if (_currentUser.Rol != RolUsuario.Administrador && !_currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("El usuario no tiene una sede asignada.");

        if (_currentUser.SedeId.HasValue && !servicio.SedesIds.Contains(_currentUser.SedeId.Value))
            throw new ForbiddenAccessException("No tiene permisos para consultar este servicio.");

        return servicio;
    }
}
