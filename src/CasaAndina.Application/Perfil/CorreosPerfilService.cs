using System.Data;
using System.Text.Json;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Perfil;

public sealed record CorreoPerfilRequest(string Correo);
public sealed record CorreoAccesoRequest(string Correo, string Password);

public sealed class CorreosPerfilService(IApplicationDbContext db, ICurrentUserService actor)
{
    private async Task<Usuario> Cliente(CancellationToken ct)
    {
        if (actor.UsuarioId is not int usuarioId || actor.ClienteId is not int clienteId) throw new ForbiddenAccessException("Se requiere una cuenta de cliente.");
        return await db.Usuarios.Include(u => u.Persona).ThenInclude(p => p.Contactos)
            .SingleOrDefaultAsync(u => u.Id == usuarioId && u.Activo && u.Persona.Cliente != null && u.Persona.Cliente.Id == clienteId && u.Persona.Cliente.Estado == "Activo", ct)
            ?? throw new ForbiddenAccessException("La cuenta de cliente no está disponible.");
    }

    private static string Normalizar(string? value)
    {
        var correo = value?.Trim().ToLowerInvariant() ?? "";
        var validation = new InlineValidator<string>();
        validation.RuleFor(x => x).NotEmpty().MaximumLength(150).EmailAddress();
        if (!validation.Validate(correo).IsValid) throw new ConflictException("Ingresa un correo válido de hasta 150 caracteres.");
        return correo;
    }

    private async Task AsegurarDisponible(string correo, Usuario usuario, CancellationToken ct)
    {
        if (await db.Usuarios.AnyAsync(u => u.Id != usuario.Id && u.Correo.ToLower() == correo, ct) ||
            await db.PersonaContactos.AnyAsync(c => c.PersonaId != usuario.PersonaId && c.TipoContacto == "Correo" && c.Valor.ToLower() == correo, ct))
            throw new ConflictException("Ese correo ya está asociado a otra persona o cuenta.");
    }

    public async Task Agregar(string correoInput, CancellationToken ct)
    {
        var correo = Normalizar(correoInput);
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var usuario = await Cliente(ct);
        await AsegurarDisponible(correo, usuario, ct);
        var correos = usuario.Persona.Contactos.Where(c => c.TipoContacto == "Correo").ToArray();
        if (correos.Length >= 10) throw new ConflictException("Puedes registrar hasta diez correos de contacto.");
        if (correos.Any(c => string.Equals(c.Valor, correo, StringComparison.OrdinalIgnoreCase))) throw new ConflictException("Este correo ya está registrado en tu perfil.");
        usuario.Persona.Contactos.Add(new PersonaContacto { TipoContacto = "Correo", Valor = correo, EsPrincipal = !correos.Any(c => c.EsPrincipal), Verificado = false });
        Audit(usuario, "AGREGAR_CORREO", null, correo);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task Principal(string correoInput, CancellationToken ct)
    {
        var correo = Normalizar(correoInput);
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var usuario = await Cliente(ct);
        var correos = usuario.Persona.Contactos.Where(c => c.TipoContacto == "Correo").ToArray();
        var nuevo = correos.SingleOrDefault(c => string.Equals(c.Valor, correo, StringComparison.OrdinalIgnoreCase)) ?? throw new NotFoundException("Correo", correo);
        var anterior = correos.FirstOrDefault(c => c.EsPrincipal);
        if (nuevo.EsPrincipal) return;
        // El índice filtrado exige quitar el principal anterior antes de asignar el nuevo.
        if (anterior is not null) { anterior.EsPrincipal = false; await db.SaveChangesAsync(ct); }
        nuevo.EsPrincipal = true;
        Audit(usuario, "CAMBIAR_CORREO_PRINCIPAL", anterior?.Valor, nuevo.Valor);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task Acceso(CorreoAccesoRequest input, IPasswordHasher hasher, CancellationToken ct)
    {
        var correo = Normalizar(input.Correo);
        if (string.IsNullOrEmpty(input.Password) || input.Password.Length > 72) throw new ConflictException("Ingresa tu contraseña actual.");
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var usuario = await Cliente(ct);
        if (!hasher.Verify(input.Password, usuario.PasswordHash)) throw new ConflictException("La contraseña actual no es correcta.");
        await AsegurarDisponible(correo, usuario, ct);
        if (string.Equals(usuario.Correo, correo, StringComparison.OrdinalIgnoreCase)) throw new ConflictException("Este correo ya es tu correo de acceso.");
        var anterior = usuario.Correo; usuario.Correo = correo;
        Audit(usuario, "CAMBIAR_CORREO_ACCESO", anterior, correo);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private void Audit(Usuario usuario, string accion, string? antes, string? despues) => db.Auditoria.Add(new Auditoria {
        FechaUtc = DateTime.UtcNow, UsuarioId = usuario.Id, SedeId = usuario.SedeId, Accion = accion == "AGREGAR_CORREO" ? "INSERT" : "UPDATE", Entidad = "PerfilCorreo",
        EntidadId = usuario.Id.ToString(), Antes = JsonSerializer.Serialize(new { Correo = antes }), Despues = JsonSerializer.Serialize(new { Operacion = accion, Correo = despues }) });
}
