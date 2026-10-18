using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Sedes.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Sedes.Queries;

public sealed record GetSedeByIdQuery(int Id) : IRequest<SedeDto>;

public sealed class GetSedeByIdQueryHandler : IRequestHandler<GetSedeByIdQuery, SedeDto>
{
    private readonly IApplicationDbContext _context;

    public GetSedeByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<SedeDto> Handle(GetSedeByIdQuery request, CancellationToken cancellationToken)
    {
        var sede = await _context.Sedes
            .AsNoTracking()
            .Where(s => s.Activo && s.Id == request.Id)
            .Select(s => new SedeDto(
                s.Id,
                s.Codigo,
                s.Nombre,
                s.Region,
                s.Ciudad,
                s.Direccion,
                s.Categoria,
                s.Telefono))
            .SingleOrDefaultAsync(cancellationToken);

        return sede ?? throw new NotFoundException("Sede", request.Id);
    }
}
