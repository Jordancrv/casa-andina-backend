using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
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
        RuleFor(x => x.Numero).NotEmpty().MaximumLength(10)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("El número de habitación no puede contener solo espacios.");
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
        var habitacion = await _context.Habitaciones.Include(h => h.Comodidades).Include(h => h.Imagenes)
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
        var foto = string.IsNullOrWhiteSpace(request.FotoUrl) ? null : request.FotoUrl.Trim();
        var principal = habitacion.Imagenes.FirstOrDefault(i => i.Orden == 1);
        if (foto is null && principal is not null) habitacion.Imagenes.Remove(principal);
        else if (foto is not null && principal is null) habitacion.Imagenes.Add(new HabitacionImagen { Url = foto, Orden = 1 });
        else if (principal is not null) principal.Url = foto!;
        habitacion.FotoUrl = foto;
        habitacion.FechaActualizacion = DateTime.UtcNow;
        habitacion.Comodidades.Clear();
        foreach (var comodidad in comodidades) habitacion.Comodidades.Add(comodidad);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
