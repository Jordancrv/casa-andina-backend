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
        RuleFor(x => x.Piso).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PrecioBase).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Estado).NotEmpty().Must(e => new[] { "Disponible", "Ocupada", "Mantenimiento", "Bloqueada" }.Contains(e));
    }
}

public class CreateHabitacionCommandHandler : IRequestHandler<CreateHabitacionCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateHabitacionCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<int> Handle(CreateHabitacionCommand request, CancellationToken cancellationToken)
    {
        var habitacion = new Habitacion
        {
            SedeId = request.SedeId,
            TipoHabitacionId = request.TipoHabitacionId,
            Numero = request.Numero,
            Piso = request.Piso,
            PrecioNoche = request.PrecioBase,
            Estado = string.IsNullOrWhiteSpace(request.Estado) ? "Disponible" : request.Estado,
            FotoUrl = request.FotoUrl,
            Activo = true
        };

        if (request.ComodidadesIds != null && request.ComodidadesIds.Count > 0)
        {
            var comodidades = await _context.Comodidades
                .Where(c => request.ComodidadesIds.Contains(c.Id))
                .ToListAsync(cancellationToken);

            foreach (var com in comodidades)
            {
                habitacion.Comodidades.Add(com);
            }
        }

        _context.Habitaciones.Add(habitacion);
        await _context.SaveChangesAsync(cancellationToken);

        return habitacion.Id;
    }
}
