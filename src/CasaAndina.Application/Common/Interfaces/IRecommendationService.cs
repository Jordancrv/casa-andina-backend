using CasaAndina.Domain.Enums;
using CasaAndina.Application.Chatbot.DTOs;
using CasaAndina.Application.Chatbot.Queries;

namespace CasaAndina.Application.Common.Interfaces;

/// <summary>
/// Puerto del planificador: catálogo verificado, clima y explicación de IA.
/// La implementación y los proveedores externos viven en Infrastructure.
/// RNF04 (&lt; 2.5s) queda pendiente de medir con el proveedor real; los timeouts
/// de resiliencia no equivalen a una garantía de latencia.
/// </summary>
public interface IRecommendationService
{
    Task<IReadOnlyList<DestinoRecomendadoDto>> RecomendarDestinosAsync(
        NivelPresupuesto presupuesto,
        TipoClima clima,
        CancellationToken cancellationToken = default);

    Task<PlanViajeDto> PlanificarAsync(PlanificarViajeQuery consulta, CancellationToken cancellationToken);
}
