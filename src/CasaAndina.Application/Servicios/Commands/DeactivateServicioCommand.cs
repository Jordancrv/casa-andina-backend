using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Servicios.Commands;

public record DeactivateServicioCommand(int Id) : IRequest;

public class DeactivateServicioCommandHandler : IRequestHandler<DeactivateServicioCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeactivateServicioCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeactivateServicioCommand request, CancellationToken cancellationToken)
    {
        var servicio = await _context.Servicios.Include(s => s.Sedes)
            .SingleOrDefaultAsync(s => s.Id == request.Id && s.Activo, cancellationToken);
        if (servicio is null)
            throw new NotFoundException("Servicio", request.Id);

        ServicioRules.ValidarServicioAsignado(_currentUser, servicio.Sedes.Select(s => s.Id).ToList());
        servicio.Activo = false;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
