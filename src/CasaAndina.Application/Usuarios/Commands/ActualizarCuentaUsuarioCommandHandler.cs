using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Usuarios.DTOs;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Usuarios.Commands;

public sealed class ActualizarCuentaUsuarioCommandValidator : AbstractValidator<ActualizarCuentaUsuarioCommand>
{
    public ActualizarCuentaUsuarioCommandValidator()
    {
        RuleFor(command => command.UsuarioId).GreaterThan(0);
        RuleFor(command => command.Correo).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(command => command.Telefono)
            .Matches(@"^\+?[0-9]{8,15}$")
            .When(command => !string.IsNullOrWhiteSpace(command.Telefono));
    }
}

public sealed class ActualizarCuentaUsuarioCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<ActualizarCuentaUsuarioCommand>
{
    public async Task Handle(ActualizarCuentaUsuarioCommand request, CancellationToken cancellationToken)
    {
        EnsureAdministrator();
        if (currentUser.Rol != RolUsuario.Superadministrador && !currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("La cuenta administrativa no tiene una sede asignada.");
        var usuario = await context.Usuarios
            .Include(item => item.Persona)
            .ThenInclude(persona => persona.Contactos)
            .SingleOrDefaultAsync(item => item.Id == request.UsuarioId, cancellationToken)
            ?? throw new NotFoundException("Usuario", request.UsuarioId);
        await EnsureSameScope(usuario, cancellationToken);

        var correo = request.Correo.Trim().ToLowerInvariant();
        var correoEnUso = await context.Usuarios.AnyAsync(otro =>
            otro.Id != usuario.Id && otro.Correo == correo, cancellationToken);
        if (correoEnUso)
            throw new CasaAndina.Application.Common.Exceptions.ValidationException(
                [new ValidationFailure(nameof(request.Correo), "El correo de acceso ya pertenece a otra cuenta.")]);

        usuario.Correo = correo;

        if (!string.IsNullOrWhiteSpace(request.Telefono))
        {
            var telefono = request.Telefono.Trim();
            var telefonoPrincipal = usuario.Persona.Contactos
                .Where(contacto => contacto.TipoContacto == "Telefono")
                .OrderByDescending(contacto => contacto.EsPrincipal)
                .FirstOrDefault();
            if (telefonoPrincipal is null)
            {
                usuario.Persona.Contactos.Add(new PersonaContacto
                {
                    PersonaId = usuario.PersonaId,
                    TipoContacto = "Telefono",
                    Valor = telefono,
                    EsPrincipal = true
                });
            }
            else
            {
                telefonoPrincipal.Valor = telefono;
                telefonoPrincipal.EsPrincipal = true;
            }
        }

        context.Auditoria.Add(new Auditoria
        {
            FechaUtc = DateTime.UtcNow,
            UsuarioId = currentUser.UsuarioId,
            SedeId = usuario.SedeId,
            Accion = "UPDATE",
            Entidad = "Usuario",
            EntidadId = usuario.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSameScope(Usuario usuario, CancellationToken cancellationToken)
    {
        if (currentUser.SedeId is not int sedeId || usuario.SedeId == sedeId)
            return;
        if (usuario.SedeId.HasValue)
            throw new NotFoundException("Usuario", usuario.Id);

        var clienteTieneReservaEnSede = await context.Clientes.AnyAsync(cliente =>
            cliente.PersonaId == usuario.PersonaId &&
            cliente.Reservas.Any(reserva => reserva.SedeId == sedeId), cancellationToken);
        if (!clienteTieneReservaEnSede)
            throw new NotFoundException("Usuario", usuario.Id);
    }

    private void EnsureAdministrator()
    {
        if (currentUser.Rol is not (RolUsuario.Administrador or RolUsuario.Superadministrador))
            throw new ForbiddenAccessException("No tiene permisos para editar usuarios.");
    }
}
