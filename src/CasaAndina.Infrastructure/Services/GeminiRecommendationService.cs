using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Infrastructure.Services;

/// <summary>
/// STUB inicial: hoy resuelve la recomendación con una consulta simple sobre Sedes/
/// Habitaciones para no bloquear el desarrollo del resto del flujo (Fase 5 del
/// cronograma). Reemplazar el cuerpo de RecomendarDestinosAsync por la llamada real
/// a la API de Gemini cuando se integre el proveedor de IA, sin tocar Application
/// ni los controllers — solo esta clase implementa IRecommendationService.
/// </summary>
public class GeminiRecommendationService : IRecommendationService
{
    private readonly Persistence.ApplicationDbContext _context;

    public GeminiRecommendationService(Persistence.ApplicationDbContext context)
        => _context = context;

    public async Task<IReadOnlyList<DestinoRecomendadoDto>> RecomendarDestinosAsync(
        NivelPresupuesto presupuesto, TipoClima clima, CancellationToken cancellationToken = default)
    {
        // TODO Fase 5: reemplazar por prompt + llamada HTTP a Gemini con estos parámetros
        // y el catálogo de sedes/habitaciones como contexto (RAG), respetando RNF04
        // (tiempo de respuesta < 2.5s).
        var sedes = await _context.Sedes
            .Where(s => s.Activo)
            .Include(s => s.Habitaciones)
            .Take(3)
            .ToListAsync(cancellationToken);

        return sedes.Select(s => new DestinoRecomendadoDto(
            s.Nombre,
            s.Ciudad,
            s.Habitaciones.Any() ? s.Habitaciones.Min(h => h.PrecioNoche) : 0,
            s.Habitaciones.FirstOrDefault()?.FotoUrl,
            new[] { presupuesto.ToString(), clima.ToString() }
        )).ToList();
    }
}
