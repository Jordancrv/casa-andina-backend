using CasaAndina.Application.Auth.DTOs;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Auth.Commands;

public record LoginCommand(string Email, string Password, string? CaptchaToken = null) : IRequest<LoginResponse>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(72);
        RuleFor(x => x.CaptchaToken).MaximumLength(8192);
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IRecaptchaVerifier _recaptchaVerifier;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IRecaptchaVerifier recaptchaVerifier)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _recaptchaVerifier = recaptchaVerifier;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (!await _recaptchaVerifier.VerifyAsync(request.CaptchaToken, cancellationToken))
            throw new UnauthorizedAccessException("Credenciales inválidas.");

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
                .Select(x => RolCodigo.Mapear(x.Rol.Codigo))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .OrderBy(RolCodigo.Prioridad)
                .ToArray();
            if (roles.Length == 0)
                throw new UnauthorizedAccessException("Credenciales inválidas.");
            var token = _jwtService.GenerarToken(usuario);
            usuario.UltimoAcceso = DateTime.UtcNow;
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
