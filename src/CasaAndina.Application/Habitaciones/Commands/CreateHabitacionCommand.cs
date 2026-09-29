using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Habitaciones.Commands;

public record CreateHabitacionCommand(
    int SedeId,
    int TipoHabitacionId,
    string Numero,
    int Piso,
    decimal PrecioBase,
    string Estado,
    string? FotoUrl,
    List<int> ComodidadesIds) : IRequest<int>;

public class CreateHabitacionCommandValidator : AbstractValidator<CreateHabitacionCommand>
{
    public CreateHabitacionCommandValidator()
    {
        RuleFor(x => x.SedeId).GreaterThan(0);
        RuleFor(x => x.TipoHabitacionId).GreaterThan(0);
        RuleFor(x => x.Numero).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Piso).InclusiveBetween(0, 255);
        RuleFor(x => x.PrecioBase).InclusiveBetween(0, 99_999_999.99m).PrecisionScale(10, 2, false);
        RuleFor(x => x.Estado).NotEmpty().Must(HabitacionRules.EstadosPermitidos.Contains)
            .WithMessage("El estado de la habitación no es válido.");
        RuleFor(x => x.FotoUrl).MaximumLength(500);
        RuleFor(x => x.ComodidadesIds).NotNull();
    }
}

public class CreateHabitacionCommandHandler : IRequestHandler<CreateHabitacionCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateHabitacionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateHabitacionCommand request, CancellationToken cancellationToken)
    {
        HabitacionRules.ValidarAccesoASede(_currentUser, request.SedeId);

        var sedeExiste = await _context.Sedes.AnyAsync(
            s => s.Id == request.SedeId && s.Activo, cancellationToken);
        if (!sedeExiste)
            throw new NotFoundException("Sede", request.SedeId);

        var tipoExiste = await _context.TiposHabitacion.AnyAsync(
            t => t.Id == request.TipoHabitacionId, cancellationToken);
        if (!tipoExiste)
            throw new NotFoundException("TipoHabitacion", request.TipoHabitacionId);

        var numero = request.Numero.Trim();
        var numeroDuplicado = await _context.Habitaciones.AnyAsync(
            h => h.SedeId == request.SedeId && h.Numero == numero, cancellationToken);
        if (numeroDuplicado)
            throw new ConflictException($"La habitación {numero} ya existe en la sede seleccionada.");

        var comodidadesIds = request.ComodidadesIds.Distinct().ToList();
        var comodidades = await _context.Comodidades
            .Where(c => comodidadesIds.Contains(c.Id))
            .ToListAsync(cancellationToken);
        if (comodidades.Count != comodidadesIds.Count)
            throw new NotFoundException("Comodidad", "uno o más identificadores");

        var habitacion = new Habitacion
        {
            SedeId = request.SedeId,
            TipoHabitacionId = request.TipoHabitacionId,
            Numero = numero,
            Piso = request.Piso,
            PrecioNoche = request.PrecioBase,
            Estado = request.Estado,
            FotoUrl = string.IsNullOrWhiteSpace(request.FotoUrl) ? null : request.FotoUrl.Trim(),
            Activo = true
        };

        foreach (var comodidad in comodidades)
        {
            habitacion.Comodidades.Add(comodidad);
        }

        _context.Habitaciones.Add(habitacion);
        await _context.SaveChangesAsync(cancellationToken);

        return habitacion.Id;
    }
}
