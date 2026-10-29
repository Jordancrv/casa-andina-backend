using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Comodidades;

public sealed record CrearComodidadCommand(string Nombre) : IRequest<int>;

public sealed class CrearComodidadValidator : AbstractValidator<CrearComodidadCommand>
{
    public CrearComodidadValidator() => RuleFor(x => x.Nombre).NotEmpty().MaximumLength(60)
        .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Indica el nombre de la comodidad.");
}

public sealed class CrearComodidadHandler(IApplicationDbContext context, ICurrentUserService user)
    : IRequestHandler<CrearComodidadCommand, int>
{
    public async Task<int> Handle(CrearComodidadCommand request, CancellationToken cancellationToken)
    {
        if (user.Rol != RolUsuario.Superadministrador && !user.SedeId.HasValue)
            throw new ForbiddenAccessException("El usuario no tiene una sede asignada.");
        var nombre = request.Nombre.Trim();
        if (await context.Comodidades.AnyAsync(c => c.Nombre.ToLower() == nombre.ToLower(), cancellationToken))
            throw new ConflictException("Ya existe una comodidad con ese nombre.");
        var comodidad = new Comodidad { Nombre = nombre };
        context.Comodidades.Add(comodidad);
        await context.SaveChangesAsync(cancellationToken);
        return comodidad.Id;
    }
}
