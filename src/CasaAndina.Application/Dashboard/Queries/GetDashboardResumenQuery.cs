using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Dashboard.DTOs;
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
        var totalHabitaciones = await _context.Habitaciones
            .CountAsync(h => h.Activo, cancellationToken);

        // Reservas activas = Pendiente o Confirmada
        var reservasActivas = await _context.Reservas
            .CountAsync(r => r.Estado == "Pendiente" || r.Estado == "Confirmada", cancellationToken);

        // Habitaciones ocupadas = las que tienen reserva Confirmada en curso hoy
        var hoy = DateTime.UtcNow.Date;
        var habitacionesOcupadas = await _context.Reservas
            .Where(r => r.Estado == "Confirmada"
                     && r.FechaCheckIn.Date <= hoy
                     && r.FechaCheckOut.Date > hoy)
            .Select(r => r.HabitacionId)
            .Distinct()
            .CountAsync(cancellationToken);

        // Ingresos del mes actual (excluyendo canceladas)
        var ingresosMes = await _context.Reservas
            .Where(r => r.FechaCreacion.Month == DateTime.UtcNow.Month
                     && r.FechaCreacion.Year == DateTime.UtcNow.Year
                     && r.Estado != "Cancelada")
            .SumAsync(r => (decimal?)r.PrecioTotal, cancellationToken) ?? 0;

        return new DashboardResumenDto(
            habitacionesOcupadas,
            totalHabitaciones - habitacionesOcupadas,
            reservasActivas,
            ingresosMes);
    }
}
