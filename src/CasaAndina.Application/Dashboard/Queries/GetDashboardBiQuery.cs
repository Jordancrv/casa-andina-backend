using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common;
using CasaAndina.Application.Dashboard.DTOs;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Dashboard.Queries;

public sealed record GetDashboardBiQuery(string Periodo = "30d") : IRequest<DashboardBiDto>;

public sealed class GetDashboardBiQueryValidator : AbstractValidator<GetDashboardBiQuery>
{
    public GetDashboardBiQueryValidator()
    {
        RuleFor(query => query.Periodo).Must(periodo => periodo is "7d" or "30d" or "mes")
            .WithMessage("El periodo debe ser 7d, 30d o mes.");
    }
}

public sealed class GetDashboardBiQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDashboardBiQuery, DashboardBiDto>
{
    public async Task<DashboardBiDto> Handle(GetDashboardBiQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("El dashboard requiere una sede asignada.");

        var sedeId = currentUser.SedeId.Value;
        var generadoUtc = DateTime.UtcNow;
        var hoy = PeruBusinessTime.FromUtc(generadoUtc).Date;
        var (inicioPeriodoLocal, finPeriodoLocal, inicioAnteriorLocal, finAnteriorLocal) = ObtenerRangos(request.Periodo, hoy);
        var inicioPeriodo = PeruBusinessTime.UtcStartOfDay(inicioPeriodoLocal);
        var finPeriodo = PeruBusinessTime.UtcStartOfDay(finPeriodoLocal);
        var inicioAnterior = PeruBusinessTime.UtcStartOfDay(inicioAnteriorLocal);
        var finAnterior = PeruBusinessTime.UtcStartOfDay(finAnteriorLocal);
        var reservasSede = context.Reservas.AsNoTracking().Where(reserva => reserva.SedeId == sedeId);
        var reservasPeriodo = reservasSede.Where(reserva => reserva.FechaCreacion >= inicioPeriodo && reserva.FechaCreacion < finPeriodo);
        var reservasAnterior = reservasSede.Where(reserva => reserva.FechaCreacion >= inicioAnterior && reserva.FechaCreacion < finAnterior);

        var totalReservas = await reservasPeriodo.CountAsync(cancellationToken);
        var totalReservasAnterior = await reservasAnterior.CountAsync(cancellationToken);
        var importeReservado = await reservasPeriodo.Where(reserva => reserva.Estado != "Cancelada")
            .SumAsync(reserva => (decimal?)reserva.PrecioTotal, cancellationToken) ?? 0;
        var importeReservadoAnterior = await reservasAnterior.Where(reserva => reserva.Estado != "Cancelada")
            .SumAsync(reserva => (decimal?)reserva.PrecioTotal, cancellationToken) ?? 0;
        var clientesActivos = await reservasPeriodo.Where(reserva => reserva.Estado != "Cancelada")
            .Select(reserva => reserva.ClienteId).Distinct().CountAsync(cancellationToken);
        var clientesAnteriores = await reservasAnterior.Where(reserva => reserva.Estado != "Cancelada")
            .Select(reserva => reserva.ClienteId).Distinct().CountAsync(cancellationToken);

        var habitacionesActivas = await context.Habitaciones
            .CountAsync(habitacion => habitacion.SedeId == sedeId && habitacion.Activo, cancellationToken);
        var habitacionesOcupadas = await context.Habitaciones
            .CountAsync(habitacion => habitacion.Activo && habitacion.SedeId == sedeId
                && (habitacion.Estado == "Ocupada"
                    || context.ReservaHabitaciones.Any(detalle => detalle.HabitacionId == habitacion.Id
                        && detalle.Reserva.SedeId == sedeId && detalle.Reserva.Estado == "Reservado"
                        && detalle.FechaCheckIn <= hoy && detalle.FechaCheckOut > hoy)), cancellationToken);
        var ocupacion = habitacionesActivas == 0 ? 0 : decimal.Round((decimal)habitacionesOcupadas * 100 / habitacionesActivas, 1);
        var habitacionesDisponibles = await context.Habitaciones
            .CountAsync(habitacion => habitacion.Activo && habitacion.SedeId == sedeId
                && habitacion.Estado == "Disponible"
                && !context.ReservaHabitaciones.Any(detalle => detalle.HabitacionId == habitacion.Id
                    && (detalle.Reserva.Estado == "Pendiente" || detalle.Reserva.Estado == "Reservado" || detalle.Reserva.Estado == "Bloqueada")
                    && detalle.FechaCheckIn <= hoy && detalle.FechaCheckOut > hoy), cancellationToken);

        var inicioSemanas = InicioSemana(hoy).AddDays(-35);
        var datosSemanalesUtc = await reservasSede
            .Where(reserva => reserva.FechaCreacion >= PeruBusinessTime.UtcStartOfDay(inicioSemanas)
                && reserva.FechaCreacion < PeruBusinessTime.UtcStartOfDay(hoy.AddDays(1))
                && (reserva.Estado == "Reservado" || reserva.Estado == "Cancelada"))
            .Select(reserva => new { reserva.FechaCreacion, reserva.Estado })
            .ToListAsync(cancellationToken);
        var datosSemanales = datosSemanalesUtc.Select(reserva => new
        {
            FechaCreacion = PeruBusinessTime.FromUtc(reserva.FechaCreacion),
            reserva.Estado
        }).ToArray();
        var series = Enumerable.Range(0, 6).Select(indice =>
        {
            var inicio = inicioSemanas.AddDays(indice * 7);
            var fin = inicio.AddDays(7);
            var confirmadas = datosSemanales.Count(reserva => reserva.Estado == "Reservado" && reserva.FechaCreacion >= inicio && reserva.FechaCreacion < fin);
            var canceladas = datosSemanales.Count(reserva => reserva.Estado == "Cancelada" && reserva.FechaCreacion >= inicio && reserva.FechaCreacion < fin);
            var etiqueta = indice == 5 ? "Actual" : $"S-{5 - indice}";
            return (etiqueta, confirmadas, canceladas);
        }).ToArray();
        var maximoSemanal = Math.Max(series.SelectMany(serie => new[] { serie.confirmadas, serie.canceladas }).DefaultIfEmpty().Max(), 1);
        var reservasSemana = series.Select(serie => new ReservaSemanaBiDto(
            serie.etiqueta, serie.confirmadas, serie.canceladas,
            decimal.Round((decimal)serie.confirmadas * 100 / maximoSemanal, 1),
            decimal.Round((decimal)serie.canceladas * 100 / maximoSemanal, 1))).ToArray();

        var distribucionCanal = await reservasPeriodo.Where(reserva => reserva.Estado != "Cancelada")
            .GroupBy(reserva => reserva.Canal)
            .Select(grupo => new { Canal = grupo.Key, Cantidad = grupo.Count() })
            .ToListAsync(cancellationToken);
        var directas = distribucionCanal.Where(item => item.Canal == "Directo").Sum(item => item.Cantidad);
        var ota = distribucionCanal.Where(item => item.Canal == "OTA").Sum(item => item.Cantidad);
        var totalCanales = distribucionCanal.Sum(item => item.Cantidad);
        var ticketPromedio = totalCanales == 0 ? 0 : decimal.Round(importeReservado / totalCanales, 2);
        var nochesReservadas = await context.ReservaHabitaciones.AsNoTracking()
            .Where(detalle => detalle.Reserva.SedeId == sedeId && detalle.Reserva.Estado != "Cancelada"
                && detalle.Reserva.FechaCreacion >= inicioPeriodo && detalle.Reserva.FechaCreacion < finPeriodo)
            .Select(detalle => new { detalle.FechaCheckIn, detalle.FechaCheckOut })
            .ToListAsync(cancellationToken);
        var estadiaPromedio = nochesReservadas.Count == 0 ? 0 : decimal.Round(
            (decimal)nochesReservadas.Sum(detalle => (detalle.FechaCheckOut.Date - detalle.FechaCheckIn.Date).Days) / nochesReservadas.Count, 1);

        var sede = await context.Sedes.AsNoTracking().Where(item => item.Id == sedeId)
            .Select(item => new { item.Nombre, item.Ciudad }).SingleAsync(cancellationToken);
        var ocupacionSede = new OcupacionSedeBiDto(sede.Nombre, sede.Ciudad, ocupacion, habitacionesOcupadas);
        var canceladasPeriodo = await reservasPeriodo.CountAsync(reserva => reserva.Estado == "Cancelada", cancellationToken);
        // Las noches corresponden a la estadía, no a la fecha de creación de la reserva.
        var inventario = await context.Habitaciones.AsNoTracking().Where(h => h.SedeId == sedeId && h.Activo)
            .OrderBy(h => h.Numero).ThenBy(h => h.Id)
            .Select(h => new { h.Id, h.Numero, Tipo = h.TipoHabitacion.Nombre }).ToListAsync(cancellationToken);
        var intervalos = await context.ReservaHabitaciones.AsNoTracking()
            .Where(d => d.Habitacion.SedeId == sedeId && d.Habitacion.Activo && d.Reserva.SedeId == sedeId
                && (d.Reserva.Estado == "Reservado" || d.Reserva.Estado == "Completada")
                && d.FechaCheckIn < finPeriodoLocal && d.FechaCheckOut > inicioPeriodoLocal)
            .Select(d => new { d.HabitacionId, d.FechaCheckIn, d.FechaCheckOut }).ToListAsync(cancellationToken);
        var dias = (finPeriodoLocal - inicioPeriodoLocal).Days;
        var intervalosPorHabitacion = intervalos.ToLookup(i => i.HabitacionId);
        var ocupacionHabitaciones = inventario.Select(h =>
        {
            var rangos = intervalosPorHabitacion[h.Id].ToArray();
            var noches = Enumerable.Range(0, dias).Count(dia => rangos.Any(i => i.FechaCheckIn.Date <= inicioPeriodoLocal.AddDays(dia)
                && i.FechaCheckOut.Date > inicioPeriodoLocal.AddDays(dia)));
            return new OcupacionHabitacionBiDto(h.Id, h.Numero, h.Tipo, noches, dias, dias == 0 ? 0 : decimal.Round(noches * 100m / dias, 1));
        }).OrderByDescending(h => h.OcupacionPct)
            .ThenBy(h => long.TryParse(h.Numero, out _) ? 0 : 1)
            .ThenBy(h => long.TryParse(h.Numero, out var numero) ? numero : long.MaxValue)
            .ThenBy(h => h.Numero, StringComparer.OrdinalIgnoreCase).ThenBy(h => h.HabitacionId).ToArray();
        var serviciosAgrupados = await context.ReservaServicios.AsNoTracking()
            .Where(s => s.Reserva.SedeId == sedeId && s.Reserva.Estado != "Cancelada"
                && s.Reserva.FechaCreacion >= inicioPeriodo && s.Reserva.FechaCreacion < finPeriodo)
            .GroupBy(s => new { s.ServicioId, s.Servicio.Nombre })
            .Select(g => new { g.Key.ServicioId, g.Key.Nombre, Cantidad = g.Sum(s => s.Cantidad), Reservas = g.Count() })
            .OrderByDescending(s => s.Cantidad).ThenBy(s => s.Nombre).ThenBy(s => s.ServicioId).Take(5)
            .ToListAsync(cancellationToken);
        var serviciosPopulares = serviciosAgrupados.Select(s => new ServicioPopularBiDto(s.ServicioId, s.Nombre, s.Cantidad, s.Reservas)).ToArray();

        return new DashboardBiDto(sede.Nombre,
            new DashboardBiKpisDto(totalReservas, totalReservasAnterior, importeReservado,
                importeReservadoAnterior, ocupacion, clientesActivos, clientesAnteriores),
            reservasSemana, [ocupacionSede], canceladasPeriodo,
            new CanalReservasBiDto(directas, ota, totalCanales, ticketPromedio, estadiaPromedio),
            new DashboardPeriodoDto(request.Periodo, inicioPeriodoLocal, finPeriodoLocal.AddDays(-1),
                inicioAnteriorLocal, finAnteriorLocal.AddDays(-1), inicioSemanas, generadoUtc), ocupacionHabitaciones, serviciosPopulares);
    }

    private static (DateTime Inicio, DateTime Fin, DateTime InicioAnterior, DateTime FinAnterior) ObtenerRangos(string periodo, DateTime hoy)
    {
        var fin = hoy.AddDays(1);
        var inicio = periodo switch
        {
            "7d" => hoy.AddDays(-6),
            "mes" => new DateTime(hoy.Year, hoy.Month, 1),
            _ => hoy.AddDays(-29)
        };
        var duracion = fin - inicio;
        var finAnterior = inicio;
        return (inicio, fin, finAnterior - duracion, finAnterior);
    }

    private static DateTime InicioSemana(DateTime fecha)
    {
        var desplazamiento = ((int)fecha.DayOfWeek + 6) % 7;
        return fecha.AddDays(-desplazamiento);
    }
}
