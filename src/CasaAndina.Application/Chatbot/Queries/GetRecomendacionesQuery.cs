using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;
using MediatR;

namespace CasaAndina.Application.Chatbot.Queries;

/// <summary>
/// RF05/RF06: entrada del flujo conversacional (presupuesto + clima preferido) hacia
/// el motor de recomendación. El Handler no sabe si el proveedor es Gemini u otro:
/// solo conoce IRecommendationService.
/// </summary>
public record GetRecomendacionesQuery(
    NivelPresupuesto Presupuesto,
    TipoClima Clima) : IRequest<IReadOnlyList<DestinoRecomendadoDto>>;

public class GetRecomendacionesQueryHandler
    : IRequestHandler<GetRecomendacionesQuery, IReadOnlyList<DestinoRecomendadoDto>>
{
    private readonly IRecommendationService _recommendationService;

    public GetRecomendacionesQueryHandler(IRecommendationService recommendationService)
        => _recommendationService = recommendationService;

    public Task<IReadOnlyList<DestinoRecomendadoDto>> Handle(
        GetRecomendacionesQuery request, CancellationToken cancellationToken)
        => _recommendationService.RecomendarDestinosAsync(
            request.Presupuesto, request.Clima, cancellationToken);
}
