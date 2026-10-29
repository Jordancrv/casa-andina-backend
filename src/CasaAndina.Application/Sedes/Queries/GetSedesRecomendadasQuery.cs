using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Sedes.Queries;

public sealed record GetSedesRecomendadasQuery(DateTime Entrada, int Noches, int Huespedes, decimal Presupuesto) : IRequest<IReadOnlyList<SedeRecomendadaPublicaDto>>;
public sealed record SedeRecomendadaPublicaDto(int Id, string Nombre, string Ciudad, string Categoria, int ReservasRecientes, int HabitacionId, string TipoHabitacion, decimal PrecioNoche);

public sealed class GetSedesRecomendadasQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSedesRecomendadasQuery, IReadOnlyList<SedeRecomendadaPublicaDto>>
{
    public async Task<IReadOnlyList<SedeRecomendadaPublicaDto>> Handle(GetSedesRecomendadasQuery request, CancellationToken ct)
    {
        if (request.Entrada.TimeOfDay != TimeSpan.Zero || request.Entrada.Date < PeruBusinessTime.Today || request.Entrada.Date > PeruBusinessTime.Today.AddDays(365) || request.Noches is < 1 or > 30 || request.Huespedes is < 1 or > 8 || request.Presupuesto is <= 0 or > 10000)
            throw new CasaAndina.Application.Common.Exceptions.ValidationException([new ValidationFailure("Busqueda", "Revisa la fecha, las noches, los huéspedes y el presupuesto por noche.")]);
        var salida = request.Entrada.AddDays(request.Noches);
        var rooms = await db.Habitaciones.AsNoTracking().Where(h => h.Activo && h.Sede.Activo && (h.Estado == "Disponible" || h.Estado == "Ocupada") && h.PrecioNoche > 0 && h.PrecioNoche <= request.Presupuesto && h.TipoHabitacion.CapacidadAdultos >= request.Huespedes)
            .Where(h => !db.ReservaHabitaciones.Any(r => r.HabitacionId == h.Id && r.Reserva.Estado != "Cancelada" && r.FechaCheckIn < salida && r.FechaCheckOut > request.Entrada))
            .Select(h => new { h.Id, h.SedeId, h.Sede.Nombre, h.Sede.Ciudad, h.Sede.Categoria, Tipo = h.TipoHabitacion.Nombre, h.PrecioNoche }).ToListAsync(ct);
        if (rooms.Count == 0) return [];
        var ids = rooms.Select(r => r.SedeId).Distinct().ToArray();
        var desde = PeruBusinessTime.UtcStartOfDay(PeruBusinessTime.Today.AddDays(-90));
        var hasta = DateTime.UtcNow;
        var counts = await db.Reservas.AsNoTracking().Where(r => ids.Contains(r.SedeId) && r.FechaCreacion >= desde && r.FechaCreacion < hasta && (r.Estado == "Reservado" || r.Estado == "Completada"))
            .GroupBy(r => r.SedeId).Select(g => new { SedeId = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.SedeId, g => g.Count, ct);
        return rooms.GroupBy(r => r.SedeId).Select(g => g.OrderBy(r => r.PrecioNoche).ThenBy(r => r.Id).First())
            .OrderByDescending(r => counts.GetValueOrDefault(r.SedeId)).ThenBy(r => r.PrecioNoche).ThenBy(r => r.SedeId).Take(3)
            .Select(r => new SedeRecomendadaPublicaDto(r.SedeId, r.Nombre, r.Ciudad, r.Categoria, counts.GetValueOrDefault(r.SedeId), r.Id, r.Tipo, r.PrecioNoche)).ToArray();
    }
}
