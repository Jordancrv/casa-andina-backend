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

public sealed class ActualizarContactosUsuarioCommandValidator : AbstractValidator<ActualizarContactosUsuarioCommand>
{
    public ActualizarContactosUsuarioCommandValidator()
    {
        RuleFor(command => command.UsuarioId).GreaterThan(0);
        RuleFor(command => command.Correo).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(command => command.Telefono)
            .Matches(@"^\+?[0-9]{8,15}$")
            .When(command => !string.IsNullOrWhiteSpace(command.Telefono));
    }
}

public sealed class ActualizarContactosUsuarioCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<ActualizarContactosUsuarioCommand>
{
    public async Task Handle(ActualizarContactosUsuarioCommand request, CancellationToken cancellationToken)
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
        var correoPrincipal = usuario.Persona.Contactos
            .Where(contacto => contacto.TipoContacto == "Correo")
            .OrderByDescending(contacto => contacto.EsPrincipal)
            .FirstOrDefault();
        var correoContactoId = correoPrincipal?.Id;
        var correoEnUso = await context.PersonaContactos.AnyAsync(contacto =>
            contacto.TipoContacto == "Correo" && (!correoContactoId.HasValue || contacto.Id != correoContactoId.Value) &&
            contacto.Valor.ToLower() == correo, cancellationToken);
        if (correoEnUso)
            throw new CasaAndina.Application.Common.Exceptions.ValidationException(
                [new ValidationFailure(nameof(request.Correo), "El correo ya está asociado a otra persona.")]);

        if (correoPrincipal is null)
        {
            usuario.Persona.Contactos.Add(new PersonaContacto
            {
                PersonaId = usuario.PersonaId,
                TipoContacto = "Correo",
                Valor = correo,
                EsPrincipal = true
            });
        }
        else
        {
            correoPrincipal.Valor = correo;
            correoPrincipal.EsPrincipal = true;
        }

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
