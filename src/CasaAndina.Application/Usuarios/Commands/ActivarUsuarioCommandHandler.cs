using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Usuarios.Commands;

public sealed record ActivarUsuarioCommand(int Id) : IRequest;

public sealed class ActivarUsuarioCommandValidator : AbstractValidator<ActivarUsuarioCommand>
{
    public ActivarUsuarioCommandValidator() => RuleFor(command => command.Id).GreaterThan(0);
}

public sealed class ActivarUsuarioCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<ActivarUsuarioCommand>
{
    public async Task Handle(ActivarUsuarioCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.TienePermiso("usuarios.desactivar"))
            throw new ForbiddenAccessException("No tiene permisos para cambiar el estado de usuarios.");
        if (currentUser.Rol != RolUsuario.Superadministrador && !currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("La cuenta administrativa no tiene una sede asignada.");

        var usuario = await context.Usuarios.SingleOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Usuario", request.Id);
        if ((currentUser.SedeId is int sedeId && usuario.SedeId != sedeId) ||
            await context.Clientes.AnyAsync(c => c.PersonaId == usuario.PersonaId, cancellationToken) ||
            await context.UsuarioRoles.AnyAsync(r => r.UsuarioId == usuario.Id && r.Rol.Codigo == "CLIENTE", cancellationToken))
            throw new NotFoundException("Usuario", usuario.Id);
        if (await context.UsuarioRoles.AnyAsync(r => r.UsuarioId == usuario.Id && (r.Rol.Codigo == "ADMINISTRADOR" || r.Rol.Codigo == "SUPERADMINISTRADOR"), cancellationToken))
            throw new ForbiddenAccessException("No se puede activar una cuenta administrativa protegida.");
        if (usuario.Activo) return;

        // Reactivar vuelve a habilitar los roles activos; no debe permitir delegar más
        // permisos que los que puede asignar quien realiza la operación.
        var permisos = await context.UsuarioRoles.Where(r => r.UsuarioId == usuario.Id && r.Rol.Activo && (r.Rol.SedeId == null || r.Rol.SedeId == usuario.SedeId))
            .SelectMany(r => r.Rol.Permisos).Select(p => new { p.PermisoCodigo, p.Permiso.Delegable }).ToListAsync(cancellationToken);
        if (permisos.Any(p => !p.Delegable || !currentUser.TienePermiso(p.PermisoCodigo)))
            throw new ForbiddenAccessException("No puede reactivar una cuenta con permisos fuera de su alcance delegable.");

        usuario.Activo = true;
        context.Auditoria.Add(new Auditoria { FechaUtc = DateTime.UtcNow, UsuarioId = currentUser.UsuarioId,
            SedeId = usuario.SedeId, Accion = "UPDATE", Entidad = "Usuario",
            EntidadId = usuario.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Antes = "{\"estado\":\"Inactivo\"}", Despues = "{\"estado\":\"Activo\"}" });
        await context.SaveChangesAsync(cancellationToken);
    }
}
