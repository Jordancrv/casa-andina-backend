using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Servicios.Commands;

public record CreateServicioCommand(
    string Nombre,
    string? Descripcion,
    decimal Precio,
    List<int> SedesIds) : IRequest<int>;

public class CreateServicioCommandValidator : AbstractValidator<CreateServicioCommand>
{
    public CreateServicioCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Descripcion).MaximumLength(300);
        RuleFor(x => x.Precio).InclusiveBetween(0, 99_999_999.99m).PrecisionScale(10, 2, false);
        RuleFor(x => x.SedesIds).NotNull().NotEmpty();
    }
}

public class CreateServicioCommandHandler : IRequestHandler<CreateServicioCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateServicioCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateServicioCommand request, CancellationToken cancellationToken)
    {
        var sedesIds = ServicioRules.ValidarSedes(_currentUser, request.SedesIds);
        var sedes = await _context.Sedes
            .Where(s => sedesIds.Contains(s.Id) && s.Activo)
            .ToListAsync(cancellationToken);
        if (sedes.Count != sedesIds.Count)
            throw new NotFoundException("Sede", "uno o más identificadores");

        var nombre = request.Nombre.Trim();
        if (await _context.Servicios.AnyAsync(s => s.Nombre == nombre, cancellationToken))
            throw new ConflictException($"Ya existe un servicio llamado {nombre}.");

        var servicio = new Servicio
        {
            Nombre = nombre,
            Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim(),
            Precio = request.Precio,
            Activo = true
        };
        foreach (var sede in sedes) servicio.Sedes.Add(sede);

        _context.Servicios.Add(servicio);
        await _context.SaveChangesAsync(cancellationToken);
        return servicio.Id;
    }
}
