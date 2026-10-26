using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CasaAndina.Application.Clientes.Commands;

public sealed record ClienteCrmDatos(string TipoPersona, string Nombre, string? Apellidos,
    string? NombreComercial, int TipoDocumentoId, string NumeroDocumento,
    string? CorreoNotificacion, string? Telefono, string Estado = "Activo", int? SedeRegistroId = null);
public sealed record CrearClienteCommand(ClienteCrmDatos Datos) : IRequest<int>;
public sealed record ActualizarClienteCommand(int ClienteId, ClienteCrmDatos Datos) : IRequest;

public sealed class ClienteCrmDatosValidator : AbstractValidator<ClienteCrmDatos>
{
    public ClienteCrmDatosValidator()
    {
        RuleFor(d => d.TipoPersona).Must(v => v is "N" or "J");
        RuleFor(d => d.Nombre).NotEmpty().MaximumLength(200)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("El nombre o razón social es obligatorio.");
        RuleFor(d => d.Nombre).Matches(@"^[\p{L}\p{M}][\p{L}\p{M}\s.'’\-]*$")
            .When(d => d.TipoPersona == "N").WithMessage("El nombre solo admite letras y separadores habituales.");
        RuleFor(d => d.Apellidos).MaximumLength(100)
            .Matches(@"^[\p{L}\p{M}][\p{L}\p{M}\s.'’\-]*$").When(d => !string.IsNullOrWhiteSpace(d.Apellidos));
        RuleFor(d => d.NombreComercial).MaximumLength(150);
        RuleFor(d => d.TipoDocumentoId).GreaterThan(0);
        RuleFor(d => d.NumeroDocumento).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9]+$");
        RuleFor(d => d.CorreoNotificacion).EmailAddress().MaximumLength(150).When(d => !string.IsNullOrWhiteSpace(d.CorreoNotificacion));
        RuleFor(d => d.Telefono).Matches(@"^\+?[0-9]{7,15}$").When(d => !string.IsNullOrWhiteSpace(d.Telefono));
        RuleFor(d => d.Estado).Must(v => v is "Activo" or "Inactivo");
        RuleFor(d => d.SedeRegistroId).GreaterThan(0).When(d => d.SedeRegistroId.HasValue);
    }
}

public sealed class CrearClienteCommandValidator : AbstractValidator<CrearClienteCommand>
{
    public CrearClienteCommandValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new ClienteCrmDatosValidator());
}
public sealed class ActualizarClienteCommandValidator : AbstractValidator<ActualizarClienteCommand>
{
    public ActualizarClienteCommandValidator()
    {
        RuleFor(x => x.ClienteId).GreaterThan(0);
        RuleFor(x => x.Datos).NotNull().SetValidator(new ClienteCrmDatosValidator());
    }
}

public sealed class GuardarClienteCommandHandler(IApplicationDbContext context, ICurrentUserService user)
    : IRequestHandler<CrearClienteCommand, int>, IRequestHandler<ActualizarClienteCommand>
{
    public async Task<int> Handle(CrearClienteCommand request, CancellationToken ct)
    {
        var sede = ClientesScope.Sede(user, request.Datos.SedeRegistroId)
            ?? throw new ConflictException("Selecciona la sede en la que se registra el cliente.");
        if (!await context.Sedes.AnyAsync(s => s.Id == sede && s.Activo, ct))
            throw new ConflictException("La sede de registro no está disponible.");
        var tipo = await ValidarDocumento(request.Datos, ct);
        await ValidarDuplicados(request.Datos, null, ct);
        var cliente = new Cliente
        {
            Estado = request.Datos.Estado, SedeRegistroId = sede, FechaRegistro = DateTime.UtcNow,
            Persona = new Persona { TipoPersona = request.Datos.TipoPersona, Estado = "Activo", FechaRegistro = DateTime.UtcNow }
        };
        Aplicar(cliente, request.Datos, tipo);
        context.Clientes.Add(cliente);
        await Guardar(request.Datos, null, ct);
        return cliente.Id;
    }

    public async Task Handle(ActualizarClienteCommand request, CancellationToken ct)
    {
        var sede = ClientesScope.Sede(user);
        var cliente = await ClientesScope.Aplicar(context.Clientes, sede)
            .Include(c => c.Persona).ThenInclude(p => p.Natural)
            .Include(c => c.Persona).ThenInclude(p => p.Juridica)
            .Include(c => c.Persona).ThenInclude(p => p.Documentos)
            .Include(c => c.Persona).ThenInclude(p => p.Contactos)
            .SingleOrDefaultAsync(c => c.Id == request.ClienteId, ct)
            ?? throw new NotFoundException("Cliente", request.ClienteId);
        if (cliente.Persona.TipoPersona != request.Datos.TipoPersona)
            throw new ConflictException("El tipo de persona no puede cambiarse desde la edición del perfil.");
        if (request.Datos.SedeRegistroId != cliente.SedeRegistroId)
            throw new ConflictException("La sede de registro es un dato de origen y no se modifica desde el perfil.");
        var tipo = await ValidarDocumento(request.Datos, ct);
        await ValidarDuplicados(request.Datos, cliente.PersonaId, ct);
        Aplicar(cliente, request.Datos, tipo);
        context.Auditoria.Add(new Auditoria
        {
            FechaUtc = DateTime.UtcNow, UsuarioId = user.UsuarioId, SedeId = sede,
            Accion = "UPDATE", Entidad = "Cliente", EntidadId = cliente.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        await Guardar(request.Datos, cliente.PersonaId, ct);
    }

    private async Task<TipoDocumento> ValidarDocumento(ClienteCrmDatos datos, CancellationToken ct)
    {
        var tipo = await context.TiposDocumento.AsNoTracking().SingleOrDefaultAsync(d => d.Id == datos.TipoDocumentoId && d.Activo, ct)
            ?? throw new ConflictException("El tipo de documento no está disponible.");
        var numero = datos.NumeroDocumento.Trim();
        if (tipo.TipoPersonaPermitida != "A" && tipo.TipoPersonaPermitida != datos.TipoPersona)
            throw new ConflictException("El documento no corresponde al tipo de persona.");
        if (numero.Length < tipo.LongitudMinima || numero.Length > tipo.LongitudMaxima ||
            (tipo.Codigo is "DNI" or "RUC" && !Regex.IsMatch(numero, "^[0-9]+$")) ||
            (tipo.Codigo == "DNI" && numero.Length != 8) || (tipo.Codigo == "RUC" && numero.Length != 11))
            throw new ConflictException("Revisa el formato y la longitud del documento seleccionado.");
        return tipo;
    }

    private async Task ValidarDuplicados(ClienteCrmDatos datos, int? personaId, CancellationToken ct)
    {
        var numero = datos.NumeroDocumento.Trim().ToUpperInvariant();
        var documentoActual = personaId.HasValue
            ? await context.PersonaDocumentos.AsNoTracking().Where(d => d.PersonaId == personaId)
                .OrderByDescending(d => d.EsPrincipal).ThenBy(d => d.Id).Select(d => (int?)d.Id).FirstOrDefaultAsync(ct)
            : null;
        if (await context.PersonaDocumentos.AsNoTracking().AnyAsync(d => d.Id != documentoActual && d.TipoDocumentoId == datos.TipoDocumentoId && d.Numero.ToUpper() == numero, ct))
            throw new ConflictException("Ya existe una persona registrada con ese documento. Revisa el perfil existente.");
        var correo = Limpiar(datos.CorreoNotificacion)?.ToLowerInvariant();
        var correoActual = personaId.HasValue
            ? await context.PersonaContactos.AsNoTracking().Where(p => p.PersonaId == personaId && p.TipoContacto == "Correo")
                .OrderByDescending(p => p.EsPrincipal).ThenBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct)
            : null;
        if (correo is not null && await context.PersonaContactos.AsNoTracking().AnyAsync(p => p.Id != correoActual && p.TipoContacto == "Correo" && p.Valor.ToLower() == correo, ct))
            throw new ConflictException("El correo de notificación ya está registrado en otro contacto.");
    }

    private void Aplicar(Cliente cliente, ClienteCrmDatos datos, TipoDocumento tipo)
    {
        var persona = cliente.Persona;
        var nombre = string.Join(' ', datos.Nombre.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (persona.TipoPersona == "N")
        {
            persona.Natural ??= new PersonaNatural();
            persona.Natural.Nombres = nombre;
            persona.Natural.Apellidos = Limpiar(datos.Apellidos);
        }
        else
        {
            persona.Juridica ??= new PersonaJuridica();
            persona.Juridica.RazonSocial = nombre;
            persona.Juridica.NombreComercial = Limpiar(datos.NombreComercial);
        }
        var documento = persona.Documentos.OrderByDescending(d => d.EsPrincipal).ThenBy(d => d.Id).FirstOrDefault();
        if (documento is null) { documento = new PersonaDocumento { EsPrincipal = true }; persona.Documentos.Add(documento); }
        documento.TipoDocumentoId = tipo.Id;
        documento.Numero = datos.NumeroDocumento.Trim().ToUpperInvariant();
        ActualizarContacto(persona, "Correo", Limpiar(datos.CorreoNotificacion)?.ToLowerInvariant());
        ActualizarContacto(persona, "Telefono", Limpiar(datos.Telefono));
        cliente.Estado = datos.Estado;
    }

    private void ActualizarContacto(Persona persona, string tipo, string? valor)
    {
        var contacto = persona.Contactos.Where(p => p.TipoContacto == tipo).OrderByDescending(p => p.EsPrincipal).ThenBy(p => p.Id).FirstOrDefault();
        if (valor is null)
        {
            if (contacto is not null) { persona.Contactos.Remove(contacto); context.PersonaContactos.Remove(contacto); }
            return;
        }
        if (contacto is null) { contacto = new PersonaContacto { TipoContacto = tipo, EsPrincipal = true }; persona.Contactos.Add(contacto); }
        if (contacto.Valor != valor) contacto.Verificado = false;
        contacto.Valor = valor;
    }

    private async Task Guardar(ClienteCrmDatos datos, int? personaId, CancellationToken ct)
    {
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // También transforma en conflicto un duplicado que apareció entre la validación y el INSERT/UPDATE.
            await ValidarDuplicados(datos, personaId, ct);
            throw;
        }
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
