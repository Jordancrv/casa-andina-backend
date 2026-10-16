using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Domain.Enums;
using CasaAndina.Application.Dashboard.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Dashboard.Queries;

public record GetDashboardResumenQuery : IRequest<DashboardResumenDto>;

public class GetDashboardResumenQueryHandler
    : IRequestHandler<GetDashboardResumenQuery, DashboardResumenDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetDashboardResumenQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DashboardResumenDto> Handle(
        GetDashboardResumenQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.Rol != RolUsuario.Superadministrador && !_currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("La cuenta administrativa no tiene una sede asignada.");
        if (!_currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("El dashboard requiere una sede específica.");

        var sedeId = _currentUser.SedeId.Value;
        // Se mantienen los estados separados para alimentar el resumen y sus gráficas.
        var reservasPendientes = await _context.Reservas
            .CountAsync(r => r.SedeId == sedeId && r.Estado == "Pendiente", cancellationToken);
        var reservasConfirmadas = await _context.Reservas
            .CountAsync(r => r.SedeId == sedeId && r.Estado == "Confirmada", cancellationToken);
        var reservasActivas = reservasPendientes + reservasConfirmadas;

        // Habitaciones ocupadas = las que tienen reserva Confirmada en curso hoy
        var hoy = PeruBusinessTime.Today;
        var habitacionesOcupadas = await _context.Habitaciones
            .CountAsync(habitacion => habitacion.Activo && habitacion.SedeId == sedeId
                && (habitacion.Estado == "Ocupada"
                    || _context.ReservaHabitaciones.Any(detalle => detalle.HabitacionId == habitacion.Id
                        && detalle.Reserva.Estado == "Confirmada"
                        && detalle.FechaCheckIn <= hoy && detalle.FechaCheckOut > hoy)), cancellationToken);

        var habitacionesDisponibles = await _context.Habitaciones
            .CountAsync(habitacion => habitacion.Activo && habitacion.SedeId == sedeId
                && habitacion.Estado == "Disponible"
                && !_context.ReservaHabitaciones.Any(detalle => detalle.HabitacionId == habitacion.Id
                    && (detalle.Reserva.Estado == "Pendiente" || detalle.Reserva.Estado == "Confirmada"
                        || detalle.Reserva.Estado == "Bloqueada")
                    && detalle.FechaCheckIn <= hoy && detalle.FechaCheckOut > hoy), cancellationToken);

        // Importe reservado del mes (no equivale a pagos aprobados).
        var inicioMesUtc = PeruBusinessTime.UtcStartOfDay(new DateTime(hoy.Year, hoy.Month, 1));
        var inicioMesSiguienteUtc = PeruBusinessTime.UtcStartOfDay(new DateTime(hoy.Year, hoy.Month, 1).AddMonths(1));
        var importeReservadoMes = await _context.Reservas
            .Where(r => r.SedeId == sedeId
                     && r.FechaCreacion >= inicioMesUtc
                     && r.FechaCreacion < inicioMesSiguienteUtc
                     && r.Estado != "Cancelada")
            .SumAsync(r => (decimal?)r.PrecioTotal, cancellationToken) ?? 0;

        return new DashboardResumenDto(
            habitacionesOcupadas,
            habitacionesDisponibles,
            reservasActivas,
            importeReservadoMes,
            reservasPendientes,
            reservasConfirmadas);
    }
}
