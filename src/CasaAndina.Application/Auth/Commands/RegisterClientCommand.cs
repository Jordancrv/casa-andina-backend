using CasaAndina.Application.Auth.DTOs;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Auth.Commands;

public record RegisterClientCommand(
    string TipoPersona,
    string TipoDocumento,
    string NumeroDocumento,
    string NombreCompleto,
    string? NombreComercial,
    string Email,
    string? Telefono,
    string Password) : IRequest<LoginResponse>;

public sealed class RegisterClientCommandValidator : AbstractValidator<RegisterClientCommand>
{
    public RegisterClientCommandValidator()
    {
        RuleFor(x => x.TipoPersona)
            .Must(value => value is "N" or "J")
            .WithMessage("El tipo de persona debe ser natural o jurídica.");

        RuleFor(x => x.TipoDocumento)
            .Must((command, value) => command.TipoPersona switch
            {
                "N" => value is "DNI" or "CE" or "PASAPORTE",
                "J" => value == "RUC",
                _ => false
            })
            .WithMessage("El tipo de documento no corresponde al tipo de persona.");

        RuleFor(x => x.NombreCompleto)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(200);
        RuleFor(x => x.NombreCompleto)
            .Matches(@"^[\p{L}][\p{L}\s.'-]*$")
            .When(x => x.TipoPersona == "N")
            .WithMessage("El nombre solo puede contener letras y separadores habituales.");

        RuleFor(x => x.NombreComercial).MaximumLength(150);

        RuleFor(x => x.NumeroDocumento).NotEmpty().MaximumLength(20);
        RuleFor(x => x.NumeroDocumento).Matches(@"^\d{8}$").When(x => x.TipoDocumento == "DNI")
            .WithMessage("El DNI debe contener exactamente 8 dígitos.");
        RuleFor(x => x.NumeroDocumento).Matches(@"^[A-Za-z0-9]{9,12}$").When(x => x.TipoDocumento == "CE")
            .WithMessage("El carné de extranjería debe contener entre 9 y 12 caracteres.");
        RuleFor(x => x.NumeroDocumento).Matches(@"^[A-Za-z0-9]{6,20}$").When(x => x.TipoDocumento == "PASAPORTE")
            .WithMessage("El pasaporte debe contener entre 6 y 20 caracteres.");
        RuleFor(x => x.NumeroDocumento).Matches(@"^\d{11}$").When(x => x.TipoDocumento == "RUC")
            .WithMessage("El RUC debe contener exactamente 11 dígitos.");

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
        var documentNumber = request.NumeroDocumento.Trim().ToUpperInvariant();
        var personType = request.TipoPersona.Trim().ToUpperInvariant();
        var documentCode = request.TipoDocumento.Trim().ToUpperInvariant();

        var documentType = await _context.TiposDocumento
            .SingleOrDefaultAsync(x => x.Codigo == documentCode && x.Activo, cancellationToken)
            ?? throw new InvalidOperationException("El tipo de documento solicitado no está disponible.");

        if (documentType.TipoPersonaPermitida is not "A"
            && documentType.TipoPersonaPermitida != personType)
            throw new CasaAndina.Application.Common.Exceptions.ValidationException([
                new ValidationFailure("TipoDocumento", "El documento no corresponde al tipo de persona.")
            ]);

        var clientAlreadyExists = await _context.Usuarios
            .AnyAsync(usuario => usuario.Correo == email, cancellationToken)
            || await _context.PersonaDocumentos.AnyAsync(
                d => d.TipoDocumentoId == documentType.Id && d.Numero == documentNumber,
                cancellationToken);

        if (clientAlreadyExists)
            throw new ConflictException("Ya existe una cuenta registrada con estos datos.");

        var nombreCompleto = string.Join(' ', request.NombreCompleto.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var persona = new Persona
        {
            TipoPersona = personType,
            Estado = "Activo",
            FechaRegistro = DateTime.UtcNow
        };
        if (personType == "N")
            persona.Natural = new PersonaNatural { Nombres = nombreCompleto };
        else
            persona.Juridica = new PersonaJuridica
            {
                RazonSocial = nombreCompleto,
                NombreComercial = string.IsNullOrWhiteSpace(request.NombreComercial)
                    ? null
                    : request.NombreComercial.Trim()
            };

        persona.Documentos.Add(new PersonaDocumento
        {
            TipoDocumentoId = documentType.Id,
            Numero = documentNumber,
            PaisEmision = "Perú",
            EsPrincipal = true
        });
        var telefono = string.IsNullOrWhiteSpace(request.Telefono)
            ? null
            : request.Telefono.Trim();
        if (telefono is not null)
        {
            persona.Contactos.Add(new PersonaContacto
            {
                TipoContacto = "Telefono",
                Valor = telefono,
                EsPrincipal = true
            });
        }

        var cliente = new Cliente
        {
            Persona = persona,
            Estado = "Activo",
            FechaRegistro = DateTime.UtcNow
        };
        persona.Cliente = cliente;

        var rolCliente = await _context.Roles
            .SingleOrDefaultAsync(x => x.Codigo == "CLIENTE", cancellationToken)
            ?? throw new InvalidOperationException("El rol Cliente no está configurado.");
        var usuario = new Usuario
        {
            Correo = email,
            Persona = persona,
            PasswordHash = _passwordHasher.Hash(request.Password),
            SedeId = null,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            Roles =
            {
                new UsuarioRol { Rol = rolCliente, FechaAsignacion = DateTime.UtcNow }
            }
        };

        _context.Clientes.Add(cliente);
        _context.Usuarios.Add(usuario);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Ya existe una cuenta registrada con estos datos.");
        }

        var token = _jwtService.GenerarToken(usuario);
        return new LoginResponse(token, nombreCompleto, "Cliente");
    }
}
