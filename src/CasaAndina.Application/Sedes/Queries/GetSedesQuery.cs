using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Sedes.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Sedes.Queries;

public sealed record GetSedesQuery(string? Region = null, string? Ciudad = null)
    : IRequest<IReadOnlyList<SedeDto>>;

public sealed class GetSedesQueryHandler
    : IRequestHandler<GetSedesQuery, IReadOnlyList<SedeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSedesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<SedeDto>> Handle(
        GetSedesQuery request,
        CancellationToken cancellationToken)
    {
        var region = Normalize(request.Region);
        var ciudad = Normalize(request.Ciudad);

        return await _context.Sedes
            .AsNoTracking()
            .Where(s => s.Activo)
            .Where(s => region == null || s.Region == region)
            .Where(s => ciudad == null || s.Ciudad == ciudad)
            .OrderBy(s => s.Ciudad)
            .ThenBy(s => s.Nombre)
            .Select(s => new SedeDto(
                s.Id,
                s.Codigo,
                s.Nombre,
                s.Region,
                s.Ciudad,
                s.Direccion,
                s.Categoria,
                s.Telefono))
            .ToListAsync(cancellationToken);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
