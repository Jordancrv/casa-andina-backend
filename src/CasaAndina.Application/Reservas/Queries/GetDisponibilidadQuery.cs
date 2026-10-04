using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Reservas.DTOs;
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
                h.Comodidades.Select(c => c.Nombre).ToArray()))
            .ToListAsync(cancellationToken);
    }

    public static void ValidarFechas(int sedeId, DateTime checkIn, DateTime checkOut)
    {
        if (sedeId <= 0 || checkIn.Date < DateTime.Today || checkOut.Date <= checkIn.Date)
            throw new ValidationException([new ValidationFailure(nameof(GetDisponibilidadQuery.FechaCheckIn),
                "Indica una sede y un rango de fechas válido, con salida posterior a la entrada.")]);
    }
}
