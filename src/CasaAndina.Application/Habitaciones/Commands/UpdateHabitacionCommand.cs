using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Habitaciones.Commands;

public record UpdateHabitacionCommand : IRequest
{
    public int Id { get; init; }
    public int SedeId { get; init; }
    public int TipoHabitacionId { get; init; }
    public string Numero { get; init; } = string.Empty;
    public int Piso { get; init; }
    public decimal PrecioBase { get; init; }
    public string Estado { get; init; } = string.Empty;
    public string? FotoUrl { get; init; }
    public List<int> ComodidadesIds { get; init; } = [];
}

public class UpdateHabitacionCommandValidator : AbstractValidator<UpdateHabitacionCommand>
{
    public UpdateHabitacionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SedeId).GreaterThan(0);
        RuleFor(x => x.TipoHabitacionId).GreaterThan(0);
        RuleFor(x => x.Numero).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Piso).InclusiveBetween(0, 255);
        RuleFor(x => x.PrecioBase).InclusiveBetween(0, 99_999_999.99m).PrecisionScale(10, 2, false);
        RuleFor(x => x.Estado).Must(HabitacionRules.EstadosPermitidos.Contains);
        RuleFor(x => x.FotoUrl).MaximumLength(500);
    }
}

public class UpdateHabitacionCommandHandler : IRequestHandler<UpdateHabitacionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateHabitacionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateHabitacionCommand request, CancellationToken cancellationToken)
    {
        var habitacion = await _context.Habitaciones.Include(h => h.Comodidades)
            .SingleOrDefaultAsync(h => h.Id == request.Id && h.Activo, cancellationToken);
        if (habitacion is null)
            throw new NotFoundException("Habitacion", request.Id);

        HabitacionRules.ValidarAccesoASede(_currentUser, habitacion.SedeId);
        HabitacionRules.ValidarAccesoASede(_currentUser, request.SedeId);

        if (!await _context.Sedes.AnyAsync(s => s.Id == request.SedeId && s.Activo, cancellationToken))
            throw new NotFoundException("Sede", request.SedeId);
        if (!await _context.TiposHabitacion.AnyAsync(t => t.Id == request.TipoHabitacionId, cancellationToken))
            throw new NotFoundException("TipoHabitacion", request.TipoHabitacionId);

        var numero = request.Numero.Trim();
        if (await _context.Habitaciones.AnyAsync(
                h => h.Id != request.Id && h.SedeId == request.SedeId && h.Numero == numero, cancellationToken))
            throw new ConflictException($"La habitación {numero} ya existe en la sede seleccionada.");

        var ids = request.ComodidadesIds.Distinct().ToList();
        var comodidades = await _context.Comodidades.Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);
        if (comodidades.Count != ids.Count)
            throw new NotFoundException("Comodidad", "uno o más identificadores");

        habitacion.SedeId = request.SedeId;
        habitacion.TipoHabitacionId = request.TipoHabitacionId;
        habitacion.Numero = numero;
        habitacion.Piso = request.Piso;
        habitacion.PrecioNoche = request.PrecioBase;
        habitacion.Estado = request.Estado;
        habitacion.FotoUrl = string.IsNullOrWhiteSpace(request.FotoUrl) ? null : request.FotoUrl.Trim();
        habitacion.FechaActualizacion = DateTime.UtcNow;
        habitacion.Comodidades.Clear();
        foreach (var comodidad in comodidades) habitacion.Comodidades.Add(comodidad);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
