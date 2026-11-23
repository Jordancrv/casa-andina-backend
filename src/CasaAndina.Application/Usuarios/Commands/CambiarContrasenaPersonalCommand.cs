using System.Text.Json;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Usuarios.Commands;

public sealed record CambiarContrasenaPersonalCommand(int UsuarioId, string Contrasena) : IRequest;

public sealed class CambiarContrasenaPersonalCommandValidator : AbstractValidator<CambiarContrasenaPersonalCommand>
{
    public CambiarContrasenaPersonalCommandValidator()
    {
        RuleFor(x => x.UsuarioId).GreaterThan(0);
        RuleFor(x => x.Contrasena).Cascade(CascadeMode.Stop).NotEmpty().MinimumLength(10).MaximumLength(72)
            .Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]").Matches("[^a-zA-Z0-9]")
            .WithMessage("La contraseña debe tener entre 10 y 72 caracteres, mayúscula, minúscula, número y símbolo.");
    }
}

public sealed class CambiarContrasenaPersonalCommandHandler(IApplicationDbContext db, ICurrentUserService actor, IPasswordHasher hasher)
    : IRequestHandler<CambiarContrasenaPersonalCommand>
{
    public async Task Handle(CambiarContrasenaPersonalCommand request, CancellationToken ct)
    {
        if (actor.Rol != RolUsuario.Administrador || actor.UsuarioId is null || actor.ClienteId is not null || actor.SedeId is not int sede || !actor.TienePermiso("usuarios.editar"))
            throw new ForbiddenAccessException("Solo un administrador de sede autorizado puede cambiar contraseñas del personal.");
        var usuario = await db.Usuarios.Include(x => x.Persona).Include(x => x.Roles).ThenInclude(x => x.Rol)
            .SingleOrDefaultAsync(x => x.Id == request.UsuarioId && x.SedeId == sede, ct)
            ?? throw new NotFoundException("Usuario", request.UsuarioId);
        if (usuario.Persona.Cliente != null || await db.Clientes.AnyAsync(c => c.PersonaId == usuario.PersonaId, ct) || usuario.Roles.Any(r => r.Rol.Codigo == "CLIENTE"))
            throw new NotFoundException("Usuario", request.UsuarioId);
        if (usuario.Id == actor.UsuarioId || usuario.Roles.Any(r => r.Rol.Codigo is "ADMINISTRADOR" or "SUPERADMINISTRADOR"))
            throw new ForbiddenAccessException("No se puede cambiar la contraseña de una cuenta administrativa desde esta sección.");
        usuario.PasswordHash = hasher.Hash(request.Contrasena);
        db.Auditoria.Add(new Auditoria { FechaUtc = DateTime.UtcNow, UsuarioId = actor.UsuarioId, SedeId = sede,
            Accion = "UPDATE", Entidad = "Usuario", EntidadId = usuario.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Despues = JsonSerializer.Serialize(new { operacion = "CambiarContrasenaPersonal" }) });
        await db.SaveChangesAsync(ct);
    }
}
