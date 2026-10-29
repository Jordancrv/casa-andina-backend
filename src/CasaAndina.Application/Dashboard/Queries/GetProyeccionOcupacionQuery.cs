using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Dashboard.Queries;

public sealed record GetProyeccionOcupacionQuery : IRequest<ProyeccionOcupacionDto>;
public sealed record SemanaOcupacionDto(DateTime Inicio, DateTime Fin, int CapacidadNoches, int ReservadasNoches, int? EstimadasNoches, decimal ReservadasPct, decimal? EstimadasPct);
public sealed record ProyeccionOcupacionDto(bool Disponible, string? Motivo, int SemanasHistoria, int HabitacionesActivas, IReadOnlyList<SemanaOcupacionDto> Semanas);

public sealed class GetProyeccionOcupacionQueryHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetProyeccionOcupacionQuery, ProyeccionOcupacionDto>
{
    public async Task<ProyeccionOcupacionDto> Handle(GetProyeccionOcupacionQuery request, CancellationToken ct)
    {
        var sede = user.SedeId ?? throw new ForbiddenAccessException("La proyección requiere una sede asignada.");
        var hoy = PeruBusinessTime.Today;
        var lunes = hoy.AddDays(-((int)hoy.DayOfWeek + 6) % 7);
        var inicio = lunes.AddDays(-84);
        var fin = lunes.AddDays(35);
        var roomCount = await db.Habitaciones.CountAsync(h => h.SedeId == sede && h.Activo, ct);
        var detalles = db.ReservaHabitaciones.AsNoTracking().Where(d => d.Reserva.SedeId == sede && d.Habitacion.SedeId == sede && d.Habitacion.Activo &&
            (d.Reserva.Estado == "Reservado" || d.Reserva.Estado == "Completada"));
        var primera = await detalles.Select(d => (DateTime?)d.FechaCheckIn).MinAsync(ct);
        var rows = await detalles.Where(d => d.FechaCheckIn < fin && d.FechaCheckOut > inicio)
            .Select(d => new OcupacionRegistro(d.HabitacionId, d.FechaCheckIn, d.FechaCheckOut)).ToListAsync(ct);
        return ProyeccionOcupacion.Calcular(hoy, roomCount, primera, rows);
    }
}

public sealed record OcupacionRegistro(int HabitacionId, DateTime Entrada, DateTime Salida);

public static class ProyeccionOcupacion
{
    // Baseline: SES over observed weekly room-nights; booked nights are a floor, not an additive forecast.
    public static ProyeccionOcupacionDto Calcular(DateTime hoy, int habitaciones, DateTime? primera, IReadOnlyList<OcupacionRegistro> registros)
    {
        hoy = hoy.Date;
        var lunes = hoy.AddDays(-((int)hoy.DayOfWeek + 6) % 7);
        var semanas = primera.HasValue ? Math.Clamp((int)((lunes - primera.Value.Date).TotalDays / 7), 0, 12) : 0;
        var historiaInicio = lunes.AddDays(-7 * semanas);
        var disponibles = habitaciones > 0 && semanas >= 8 && registros.Count(r => r.Entrada.Date < lunes && r.Salida.Date > historiaInicio) >= 10;
        string? motivo = habitaciones == 0 ? "No hay habitaciones activas en la sede." : disponibles ? null : "Se requieren al menos 8 semanas de historial y 10 estadías registradas para estimar la ocupación.";
        int Noches(DateTime start, DateTime end) => Enumerable.Range(0, (end - start).Days).Sum(offset =>
        {
            var fecha = start.AddDays(offset);
            return registros.Where(r => r.Entrada.Date <= fecha && r.Salida.Date > fecha).Select(r => r.HabitacionId).Distinct().Count();
        });
        var capacidad = habitaciones * 7;
        double nivel = 0;
        if (disponibles)
        {
            for (var semana = 0; semana < semanas; semana++)
            {
                var fecha = historiaInicio.AddDays(semana * 7);
                var observado = Math.Min(capacidad, Noches(fecha, fecha.AddDays(7)));
                nivel = semana == 0 ? observado : .3 * observado + .7 * nivel;
            }
        }
        var futuras = Enumerable.Range(1, 4).Select(i =>
        {
            var fecha = lunes.AddDays(i * 7);
            var reservado = Math.Min(capacidad, Noches(fecha, fecha.AddDays(7)));
            var estimado = disponibles ? (int?)Math.Clamp((int)Math.Round(Math.Max(reservado, nivel), MidpointRounding.AwayFromZero), 0, capacidad) : null;
            return new SemanaOcupacionDto(fecha, fecha.AddDays(6), capacidad, reservado, estimado,
                capacidad == 0 ? 0 : Math.Round(100m * reservado / capacidad, 1), estimado.HasValue && capacidad > 0 ? Math.Round(100m * estimado.Value / capacidad, 1) : null);
        }).ToArray();
        return new(disponibles, motivo, semanas, habitaciones, futuras);
    }
}
