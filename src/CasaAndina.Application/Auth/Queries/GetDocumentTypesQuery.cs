using CasaAndina.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Auth.Queries;

public record DocumentTypeDto(
    string Codigo,
    string Nombre,
    int LongitudMinima,
    int LongitudMaxima);

public record GetDocumentTypesQuery(string TipoPersona) : IRequest<IReadOnlyList<DocumentTypeDto>>;

public sealed class GetDocumentTypesQueryHandler
    : IRequestHandler<GetDocumentTypesQuery, IReadOnlyList<DocumentTypeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDocumentTypesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<DocumentTypeDto>> Handle(
        GetDocumentTypesQuery request,
        CancellationToken cancellationToken)
    {
        var personType = request.TipoPersona.Trim().ToUpperInvariant();
        if (personType is not "N" and not "J")
            return [];

        return await _context.TiposDocumento
            .AsNoTracking()
            .Where(x => x.Activo
                && (x.TipoPersonaPermitida == personType || x.TipoPersonaPermitida == "A"))
            .OrderBy(x => x.Codigo == "DNI" ? 0
                : x.Codigo == "CE" ? 1
                : x.Codigo == "PASAPORTE" ? 2
                : 3)
            .Select(x => new DocumentTypeDto(
                x.Codigo,
                x.Nombre,
                x.LongitudMinima,
                x.LongitudMaxima))
            .ToListAsync(cancellationToken);
    }
}
