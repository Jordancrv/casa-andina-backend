using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Habitaciones.Commands;

public record DeactivateHabitacionCommand(int Id) : IRequest;

public class DeactivateHabitacionCommandHandler : IRequestHandler<DeactivateHabitacionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeactivateHabitacionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeactivateHabitacionCommand request, CancellationToken cancellationToken)
    {
        var habitacion = await _context.Habitaciones
            .SingleOrDefaultAsync(h => h.Id == request.Id && h.Activo, cancellationToken);
        if (habitacion is null)
            throw new NotFoundException("Habitacion", request.Id);

        HabitacionRules.ValidarAccesoASede(_currentUser, habitacion.SedeId);
        var tieneReservas = await _context.Reservas.AnyAsync(r =>
            r.HabitacionId == request.Id &&
            r.FechaCheckOut >= DateTime.UtcNow.Date &&
            (r.Estado == "Pendiente" || r.Estado == "Confirmada" || r.Estado == "Bloqueada"),
            cancellationToken);
        if (tieneReservas)
            throw new ConflictException("La habitación tiene reservas activas o futuras y no puede desactivarse.");

        habitacion.Activo = false;
        habitacion.FechaActualizacion = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
