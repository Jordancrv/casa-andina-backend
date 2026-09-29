using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Servicios.Commands;

public record UpdateServicioCommand : IRequest
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public decimal Precio { get; init; }
    public List<int> SedesIds { get; init; } = [];
}

public class UpdateServicioCommandValidator : AbstractValidator<UpdateServicioCommand>
{
    public UpdateServicioCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Descripcion).MaximumLength(300);
        RuleFor(x => x.Precio).InclusiveBetween(0, 99_999_999.99m).PrecisionScale(10, 2, false);
        RuleFor(x => x.SedesIds).NotNull().NotEmpty();
    }
}

public class UpdateServicioCommandHandler : IRequestHandler<UpdateServicioCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateServicioCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateServicioCommand request, CancellationToken cancellationToken)
    {
        var servicio = await _context.Servicios.Include(s => s.Sedes)
            .SingleOrDefaultAsync(s => s.Id == request.Id && s.Activo, cancellationToken);
        if (servicio is null)
            throw new NotFoundException("Servicio", request.Id);

        ServicioRules.ValidarServicioAsignado(_currentUser, servicio.Sedes.Select(s => s.Id).ToList());
        var sedesIds = ServicioRules.ValidarSedes(_currentUser, request.SedesIds);
        var sedes = await _context.Sedes
            .Where(s => sedesIds.Contains(s.Id) && s.Activo)
            .ToListAsync(cancellationToken);
        if (sedes.Count != sedesIds.Count)
            throw new NotFoundException("Sede", "uno o más identificadores");

        var nombre = request.Nombre.Trim();
        if (await _context.Servicios.AnyAsync(
                s => s.Id != request.Id && s.Nombre == nombre, cancellationToken))
            throw new ConflictException($"Ya existe un servicio llamado {nombre}.");

        servicio.Nombre = nombre;
        servicio.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion)
            ? null : request.Descripcion.Trim();
        servicio.Precio = request.Precio;
        servicio.Sedes.Clear();
        foreach (var sede in sedes) servicio.Sedes.Add(sede);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
