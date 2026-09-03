using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using FluentValidation;
using MediatR;

namespace CasaAndina.Application.Habitaciones.Commands;

public record CreateHabitacionCommand(
    string Numero,
    int Piso,
    decimal PrecioNoche,
    int Capacidad,
    int SedeId,
    string? FotoUrl) : IRequest<int>;

public class CreateHabitacionCommandValidator : AbstractValidator<CreateHabitacionCommand>
{
    public CreateHabitacionCommandValidator()
    {
        RuleFor(x => x.Numero).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Piso).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PrecioNoche).GreaterThan(0);
        RuleFor(x => x.Capacidad).GreaterThan(0);
        RuleFor(x => x.SedeId).GreaterThan(0);
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
            Numero = request.Numero,
            Piso = request.Piso,
            PrecioNoche = request.PrecioNoche,
            Capacidad = request.Capacidad,
            SedeId = request.SedeId,
            FotoUrl = request.FotoUrl
        };

        _context.Habitaciones.Add(habitacion);
        await _context.SaveChangesAsync(cancellationToken);

        return habitacion.Id;
    }
}
