using CasaAndina.Application.Chatbot.DTOs;
using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace CasaAndina.Application.Chatbot.Queries;

public sealed record PlanificarViajeQuery(
    string Mensaje, DateTime FechaEntrada, DateTime FechaSalida,
    decimal PresupuestoPorNoche, int Adultos = 2, string? Ciudad = null,
    IReadOnlyList<MensajeViajeDto>? Historial = null)
    : IRequest<PlanViajeDto>;

public sealed class PlanificarViajeQueryValidator : AbstractValidator<PlanificarViajeQuery>
{
    public PlanificarViajeQueryValidator()
    {
        RuleFor(x => x.Mensaje).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Ciudad).MaximumLength(100);
        RuleFor(x => x.Historial).Must(h => h is null || (h.Count <= 8 && h.All(m => m is not null
            && (m.Rol is "usuario" or "asistente") && !string.IsNullOrWhiteSpace(m.Texto) && m.Texto.Length <= 1000)))
            .WithMessage("El historial admite hasta 8 mensajes de usuario o asistente, de 1 a 1000 caracteres.");
        RuleFor(x => x.Adultos).InclusiveBetween(1, 8);
        RuleFor(x => x.PresupuestoPorNoche).InclusiveBetween(1m, 10000m);
        RuleFor(x => x.FechaEntrada)
            .Must(d => d.TimeOfDay == TimeSpan.Zero && d.Date >= PeruBusinessTime.Today && d.Date <= PeruBusinessTime.Today.AddDays(365))
            .WithMessage("La entrada debe ser una fecha sin hora, entre hoy y los próximos 365 días.");
        RuleFor(x => x.FechaSalida)
            .Must((q, d) => d.TimeOfDay == TimeSpan.Zero && d > q.FechaEntrada && (d - q.FechaEntrada).TotalDays <= 30)
            .WithMessage("La salida debe ser posterior a la entrada, con una estadía de hasta 30 noches.");
    }
}

public sealed class PlanificarViajeQueryHandler(IRecommendationService service)
    : IRequestHandler<PlanificarViajeQuery, PlanViajeDto>
{
    public Task<PlanViajeDto> Handle(PlanificarViajeQuery request, CancellationToken cancellationToken)
        => service.PlanificarAsync(request, cancellationToken);
}
