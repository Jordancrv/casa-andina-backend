using CasaAndina.Application.Auth.DTOs;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Auth.Commands;

public record RegisterClientCommand(
    string NombreCompleto,
    string Dni,
    string Email,
    string? Telefono,
    string Password) : IRequest<LoginResponse>;

public sealed class RegisterClientCommandValidator : AbstractValidator<RegisterClientCommand>
{
    public RegisterClientCommandValidator()
    {
        RuleFor(x => x.NombreCompleto)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(200)
            .Matches(@"^[\p{L}][\p{L}\s.'-]*$")
            .WithMessage("El nombre solo puede contener letras y separadores habituales.");

        RuleFor(x => x.Dni)
            .NotEmpty()
            .Matches(@"^\d{8}$")
            .WithMessage("El DNI debe contener exactamente 8 dígitos.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(150)
            .EmailAddress();

        RuleFor(x => x.Telefono)
            .Matches(@"^\+?[0-9]{7,15}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Telefono))
            .WithMessage("El teléfono debe contener entre 7 y 15 dígitos.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(72)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir un número.")
            .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña debe incluir un símbolo.");
    }
}

public sealed class RegisterClientCommandHandler : IRequestHandler<RegisterClientCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public RegisterClientCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<LoginResponse> Handle(
        RegisterClientCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var dni = request.Dni.Trim();

        var dniType = await _context.TiposDocumento
            .SingleOrDefaultAsync(x => x.Codigo == "DNI" && x.Activo, cancellationToken)
            ?? throw new InvalidOperationException("El catálogo de tipos de documento no contiene DNI.");

        var clientAlreadyExists = await _context.PersonaCorreos
            .AnyAsync(c => c.Correo == email, cancellationToken)
            || await _context.PersonaDocumentos.AnyAsync(
                d => d.TipoDocumentoId == dniType.Id && d.Numero == dni,
                cancellationToken);
        var staffEmailAlreadyExists = await _context.Usuarios
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (clientAlreadyExists || staffEmailAlreadyExists)
            throw new ConflictException("Ya existe una cuenta registrada con estos datos.");

        var nombreCompleto = string.Join(' ', request.NombreCompleto.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var persona = new Persona
        {
            TipoPersona = "N",
            Estado = "Activo",
            FechaRegistro = DateTime.UtcNow,
            Natural = new PersonaNatural
            {
                Nombres = nombreCompleto
            }
        };
        persona.Documentos.Add(new PersonaDocumento
        {
            TipoDocumentoId = dniType.Id,
            Numero = dni,
            PaisEmision = "Perú",
            EsPrincipal = true
        });
        persona.Correos.Add(new PersonaCorreo
        {
            Correo = email,
            EsPrincipal = true
        });

        var telefono = string.IsNullOrWhiteSpace(request.Telefono)
            ? null
            : request.Telefono.Trim();
        if (telefono is not null)
        {
            persona.Telefonos.Add(new PersonaTelefono
            {
                Numero = telefono,
                EsPrincipal = true
            });
        }

        var cliente = new Cliente
        {
            Persona = persona,
            NombreCompleto = nombreCompleto,
            DNI = dni,
            Correo = email,
            Telefono = telefono,
            ContrasenaHash = _passwordHasher.Hash(request.Password),
            Estado = "Activo",
            FechaRegistro = DateTime.UtcNow
        };

        _context.Clientes.Add(cliente);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Ya existe una cuenta registrada con estos datos.");
        }

        var token = _jwtService.GenerarToken(cliente);
        return new LoginResponse(token, cliente.NombreCompleto, "Cliente");
    }
}
