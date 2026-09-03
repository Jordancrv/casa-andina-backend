using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Dashboard.DTOs;
using CasaAndina.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Dashboard.Queries;

public record GetDashboardResumenQuery : IRequest<DashboardResumenDto>;

public class GetDashboardResumenQueryHandler
    : IRequestHandler<GetDashboardResumenQuery, DashboardResumenDto>
{
    private readonly IApplicationDbContext _context;

    public GetDashboardResumenQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<DashboardResumenDto> Handle(
        GetDashboardResumenQuery request, CancellationToken cancellationToken)
    {
        var totalHabitaciones = await _context.Habitaciones.CountAsync(h => h.Activo, cancellationToken);

        var reservasActivas = await _context.Reservas
            .CountAsync(r => r.Estado == EstadoReserva.Activa, cancellationToken);

        var habitacionesOcupadas = await _context.ReservaHabitaciones
            .Where(rh => rh.Reserva.Estado == EstadoReserva.Activa)
            .Select(rh => rh.HabitacionId)
            .Distinct()
            .CountAsync(cancellationToken);

        var ingresosMes = await _context.Reservas
            .Where(r => r.FechaCreacion.Month == DateTime.UtcNow.Month
                     && r.FechaCreacion.Year == DateTime.UtcNow.Year
                     && r.Estado != EstadoReserva.Cancelada)
            .SumAsync(r => (decimal?)r.MontoTotal, cancellationToken) ?? 0;

        return new DashboardResumenDto(
            habitacionesOcupadas,
            totalHabitaciones - habitacionesOcupadas,
            reservasActivas,
            ingresosMes);
    }
}
