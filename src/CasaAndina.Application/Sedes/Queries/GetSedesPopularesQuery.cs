using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Sedes.Queries;

public sealed record SedePopularDto(int Id, string Nombre, string Ciudad, string Categoria, int ReservasRecientes);
public sealed record GetSedesPopularesQuery : IRequest<IReadOnlyList<SedePopularDto>>;

public sealed class GetSedesPopularesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSedesPopularesQuery, IReadOnlyList<SedePopularDto>>
{
    public async Task<IReadOnlyList<SedePopularDto>> Handle(GetSedesPopularesQuery request, CancellationToken ct)
    {
        var desde = PeruBusinessTime.UtcStartOfDay(PeruBusinessTime.Today.AddDays(-90));
        var hasta = DateTime.UtcNow;
        return await ConstruirConsulta(db, desde, hasta).ToListAsync(ct);
    }

    public static IQueryable<SedePopularDto> ConstruirConsulta(IApplicationDbContext db, DateTime desde, DateTime hasta) =>
        db.Sedes.AsNoTracking().Where(s => s.Activo && s.Habitaciones.Any(h => h.Activo))
            .Select(s => new { s.Id, s.Nombre, s.Ciudad, s.Categoria,
                ReservasRecientes = db.Reservas.Count(r => r.SedeId == s.Id && r.FechaCreacion >= desde && r.FechaCreacion < hasta && (r.Estado == "Reservado" || r.Estado == "Completada")) })
            .Where(s => s.ReservasRecientes > 0).OrderByDescending(s => s.ReservasRecientes).ThenBy(s => s.Nombre).ThenBy(s => s.Id).Take(3)
            .Select(s => new SedePopularDto(s.Id, s.Nombre, s.Ciudad, s.Categoria, s.ReservasRecientes));
}
