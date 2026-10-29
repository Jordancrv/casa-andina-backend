using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common;
using CasaAndina.Application.Reservas.DTOs;
using CasaAndina.Domain.Enums;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Reservas.Queries;

public sealed record GetDisponibilidadQuery(int SedeId, DateTime FechaCheckIn, DateTime FechaCheckOut)
    : IRequest<IReadOnlyList<HabitacionDisponibleDto>>;

public sealed class GetDisponibilidadQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDisponibilidadQuery, IReadOnlyList<HabitacionDisponibleDto>>
{
    public async Task<IReadOnlyList<HabitacionDisponibleDto>> Handle(
        GetDisponibilidadQuery request, CancellationToken cancellationToken)
    {
        ValidarFechas(request.SedeId, request.FechaCheckIn, request.FechaCheckOut);
        var checkIn = request.FechaCheckIn.Date;
        var checkOut = request.FechaCheckOut.Date;

        return await context.Habitaciones.AsNoTracking()
            .Where(h => h.SedeId == request.SedeId && h.Activo &&
                (h.Estado == "Disponible" || h.Estado == "Ocupada"))
            .Where(h => !context.ReservaHabitaciones.Any(rh =>
                rh.HabitacionId == h.Id &&
                rh.FechaCheckIn < checkOut && rh.FechaCheckOut > checkIn &&
                rh.Reserva.Estado != "Cancelada"))
            .OrderBy(h => h.TipoHabitacion.Nombre).ThenBy(h => h.PrecioNoche)
            .Select(h => new HabitacionDisponibleDto(
                h.Id, h.SedeId, h.Sede.Nombre, h.Sede.Ciudad, h.Numero, h.Piso,
                h.TipoHabitacion.Nombre, h.TipoHabitacion.CapacidadAdultos,
                h.TipoHabitacion.CapacidadNinos, h.PrecioNoche,
                h.Comodidades.Select(c => c.Nombre).ToArray(), h.FotoUrl))
            .ToListAsync(cancellationToken);
    }

    public static void ValidarFechas(int sedeId, DateTime checkIn, DateTime checkOut)
    {
        if (sedeId <= 0 || checkIn.Date < PeruBusinessTime.Today || checkOut.Date <= checkIn.Date)
            throw new ValidationException([new ValidationFailure(nameof(GetDisponibilidadQuery.FechaCheckIn),
                "Indica una sede y un rango de fechas válido, con salida posterior a la entrada.")]);
    }
}

public sealed record GetCalendarioReservasQuery(int SedeId, DateTime FechaDesde, DateTime FechaHasta)
    : IRequest<IReadOnlyList<HabitacionCalendarioDto>>;

public sealed class GetCalendarioReservasQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetCalendarioReservasQuery, IReadOnlyList<HabitacionCalendarioDto>>
{
    public async Task<IReadOnlyList<HabitacionCalendarioDto>> Handle(
        GetCalendarioReservasQuery request, CancellationToken cancellationToken)
    {
        ValidarRango(request.SedeId, request.FechaDesde, request.FechaHasta);
        if (currentUser.Rol != RolUsuario.Superadministrador && !currentUser.SedeId.HasValue)
        {
            throw new ForbiddenAccessException("El usuario no tiene una sede asignada.");
        }

        if (currentUser.Rol != RolUsuario.Superadministrador && currentUser.SedeId != request.SedeId)
            throw new ForbiddenAccessException("No tiene permisos para consultar reservas de otra sede.");

        var desde = request.FechaDesde.Date;
        var hasta = request.FechaHasta.Date;

        return await context.Habitaciones.AsNoTracking()
            .Where(h => h.SedeId == request.SedeId && h.Activo)
            .OrderBy(h => h.Numero)
            .Select(h => new HabitacionCalendarioDto(
                h.Id, h.Numero, h.Piso, h.TipoHabitacion.Nombre, h.Estado,
                context.ReservaHabitaciones
                    .Where(rh => rh.HabitacionId == h.Id &&
                        rh.FechaCheckIn < hasta && rh.FechaCheckOut > desde &&
                        (rh.Reserva.Estado == "Pendiente" || rh.Reserva.Estado == "Reservado"))
                    .OrderBy(rh => rh.FechaCheckIn)
                    .Select(rh => new ReservaCalendarioDto(
                        rh.FechaCheckIn,
                        rh.FechaCheckOut,
                        rh.Reserva.Estado,
                        currentUser.Rol == RolUsuario.Administrador
                            ? rh.Reserva.Cliente.Persona.TipoPersona == "N"
                                ? ((rh.Reserva.Cliente.Persona.Natural!.Nombres + " ") + (rh.Reserva.Cliente.Persona.Natural.Apellidos ?? "")).Trim()
                                : (rh.Reserva.Cliente.Persona.Juridica!.NombreComercial ?? rh.Reserva.Cliente.Persona.Juridica.RazonSocial)
                            : null))
                    .ToArray()))
            .ToListAsync(cancellationToken);
    }

    public static void ValidarRango(int sedeId, DateTime desde, DateTime hasta)
    {
        if (sedeId <= 0 || hasta.Date <= desde.Date || (hasta.Date - desde.Date).TotalDays > 366)
            throw new ValidationException([new ValidationFailure(nameof(GetCalendarioReservasQuery.FechaHasta),
                "Indica una sede y un rango válido de hasta 366 días, con fecha final posterior a la inicial.")]);
    }
}
