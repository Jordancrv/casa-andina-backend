using CasaAndina.Application.Auth.DTOs;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Auth.Commands;

public record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(72);
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var usuario = await _context.Usuarios
            .Include(u => u.Roles).ThenInclude(x => x.Rol)
            .Include(u => u.Persona).ThenInclude(x => x.Natural)
            .Include(u => u.Persona).ThenInclude(x => x.Juridica)
            .Include(u => u.Persona).ThenInclude(x => x.Cliente)
            .SingleOrDefaultAsync(u => u.Correo == email && u.Activo, cancellationToken);

        if (usuario is null)
        {
            _passwordHasher.VerifyAgainstDummyHash(request.Password);
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        if (_passwordHasher.Verify(request.Password, usuario.PasswordHash))
        {
            var roles = usuario.Roles
                .Where(x => x.Rol.Activo && (x.Rol.SedeId == null || x.Rol.SedeId == usuario.SedeId))
                .Select(x => RolCodigo.Mapear(x.Rol.Codigo))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .OrderBy(RolCodigo.Prioridad)
                .ToArray();
            if (roles.Length == 0)
                throw new UnauthorizedAccessException("Credenciales inválidas.");
            var token = _jwtService.GenerarToken(usuario);
            usuario.UltimoAcceso = DateTime.UtcNow;
            if (usuario.SedeId.HasValue && roles.Any(role => role != RolUsuario.Cliente))
                _context.Auditoria.Add(new CasaAndina.Domain.Entities.Auditoria { FechaUtc = DateTime.UtcNow,
                    UsuarioId = usuario.Id, SedeId = usuario.SedeId, Accion = "LOGIN", Entidad = "Sesion",
                    EntidadId = usuario.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            await _context.SaveChangesAsync(cancellationToken);

            var nombre = NombrePersona(usuario.Persona);
            return new LoginResponse(token, nombre, roles[0].ToString());
        }

        throw new UnauthorizedAccessException("Credenciales inválidas.");
    }

    private static string NombrePersona(CasaAndina.Domain.Entities.Persona persona)
        => persona.Natural is not null
            ? string.Join(' ', new[] { persona.Natural.Nombres, persona.Natural.Apellidos }
                .Where(x => !string.IsNullOrWhiteSpace(x)))
            : persona.Juridica?.RazonSocial ?? "Usuario";
}
