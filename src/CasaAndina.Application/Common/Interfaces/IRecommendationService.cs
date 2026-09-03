using CasaAndina.Domain.Enums;

namespace CasaAndina.Application.Common.Interfaces;

/// <summary>
/// Puerto hacia el proveedor de IA (Gemini, según el acta). La implementación real
/// vive en Infrastructure — Application solo depende de esta abstracción, así se
/// puede cambiar de proveedor de IA sin tocar casos de uso.
/// RNF04: se espera respuesta &lt; 2.5s por consulta.
/// </summary>
public interface IRecommendationService
{
    Task<IReadOnlyList<DestinoRecomendadoDto>> RecomendarDestinosAsync(
        NivelPresupuesto presupuesto,
        TipoClima clima,
        CancellationToken cancellationToken = default);
}

public record DestinoRecomendadoDto(
    string SedeNombre,
    string Ciudad,
    decimal PrecioReferencial,
    string? FotoUrl,
    IReadOnlyList<string> Tags);
