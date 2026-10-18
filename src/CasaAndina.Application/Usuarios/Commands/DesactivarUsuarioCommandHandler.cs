using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Usuarios.Commands;

public sealed record DesactivarUsuarioCommand(int Id) : IRequest;

public sealed class DesactivarUsuarioCommandValidator : AbstractValidator<DesactivarUsuarioCommand>
{
    public DesactivarUsuarioCommandValidator() => RuleFor(command => command.Id).GreaterThan(0);
}

public sealed class DesactivarUsuarioCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<DesactivarUsuarioCommand>
{
    public async Task Handle(DesactivarUsuarioCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Rol is not (RolUsuario.Administrador or RolUsuario.Superadministrador))
            throw new ForbiddenAccessException("No tiene permisos para desactivar usuarios.");
        if (currentUser.UsuarioId == request.Id)
            throw new CasaAndina.Application.Common.Exceptions.ValidationException(
                [new ValidationFailure(nameof(request.Id), "No puedes desactivar tu propia cuenta.")]);
        if (currentUser.Rol != RolUsuario.Superadministrador && !currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("La cuenta administrativa no tiene una sede asignada.");

        var usuario = await context.Usuarios.SingleOrDefaultAsync(
            item => item.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Usuario", request.Id);

        if (currentUser.SedeId is int sedeId && usuario.SedeId != sedeId)
        {
            if (usuario.SedeId.HasValue)
                throw new NotFoundException("Usuario", request.Id);

            var clienteTieneReservaEnSede = await context.Clientes.AnyAsync(cliente =>
                cliente.PersonaId == usuario.PersonaId &&
                cliente.Reservas.Any(reserva => reserva.SedeId == sedeId), cancellationToken);
            if (!clienteTieneReservaEnSede)
                throw new NotFoundException("Usuario", request.Id);
        }

        if (!usuario.Activo)
            return;

        usuario.Activo = false;
        context.Auditoria.Add(new Auditoria
        {
            FechaUtc = DateTime.UtcNow,
            UsuarioId = currentUser.UsuarioId,
            SedeId = usuario.SedeId,
            Accion = "UPDATE",
            Entidad = "Usuario",
            EntidadId = usuario.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Despues = "{\"estado\":\"Inactivo\"}"
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
