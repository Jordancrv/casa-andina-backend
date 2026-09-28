using CasaAndina.Application.Auth.DTOs;
using CasaAndina.Application.Common.Interfaces;
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
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
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
        var email = request.Email.Trim();
        var usuario = await _context.Usuarios
            .Include(u => u.RolAsignado)
            .SingleOrDefaultAsync(u => u.Email == email && u.Activo, cancellationToken);

        if (usuario is not null && _passwordHasher.Verify(request.Password, usuario.PasswordHash))
        {
            usuario.UltimoAcceso = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            var token = _jwtService.GenerarToken(usuario);
            return new LoginResponse(token, usuario.NombreCompleto, usuario.Rol.ToString());
        }

        var cliente = await _context.Clientes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                c => c.Correo == email && c.Estado == "Activo" && c.ContrasenaHash != null,
                cancellationToken);

        if (cliente?.ContrasenaHash is not null
            && _passwordHasher.Verify(request.Password, cliente.ContrasenaHash))
        {
            var token = _jwtService.GenerarToken(cliente);
            return new LoginResponse(token, cliente.NombreCompleto, "Cliente");
        }

        throw new UnauthorizedAccessException("Credenciales inválidas.");
    }
}
