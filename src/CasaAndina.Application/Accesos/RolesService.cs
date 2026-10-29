using System.Data;
using System.Text.Json;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Accesos;

public sealed record GuardarRol(string Nombre, string? Descripcion, string[] Permisos, Guid? Version);
public sealed record AsignarRoles(int[] Roles, int[] RolesAnteriores);
public sealed record CrearPersonal(string NombreCompleto, string TipoDocumento, string NumeroDocumento, string Email, string? Telefono, string Password, int[] Roles);

/// <summary>Writes are serialized with assignment changes to prevent self-elevation and stale updates.</summary>
public sealed class RolesService(IApplicationDbContext db, ICurrentUserService actor)
{
    private int Sede() => actor.SedeId ?? throw new ForbiddenAccessException("Se requiere una sede asignada para administrar personal y roles locales.");
    private void Require(string permiso)
    {
        if (actor.UsuarioId is null || actor.ClienteId is not null || !actor.TienePermiso(permiso))
            throw new ForbiddenAccessException("No tiene permiso para esta acción.");
    }

    public async Task<object> Listar(CancellationToken ct)
    {
        Require("roles.ver");
        var sede = actor.SedeId;
        var roles = await db.Roles.AsNoTracking().Where(r => r.SedeId == null || r.SedeId == sede)
            .OrderByDescending(r => r.Protegido).ThenBy(r => r.Nombre)
            .Select(r => new { r.Id, r.Nombre, r.Descripcion, r.SedeId, r.Protegido, r.Activo, r.Version,
                Permisos = r.Permisos.Select(p => p.PermisoCodigo).ToArray(),
                Usuarios = r.Usuarios.Count(u => u.Usuario.SedeId == sede),
                Propio = r.Usuarios.Any(u => u.UsuarioId == actor.UsuarioId) }).ToListAsync(ct);
        var permisos = await db.Permisos.AsNoTracking().OrderBy(p => p.Seccion).ThenBy(p => p.Codigo).ToListAsync(ct);
        return new { roles, permisos = permisos.Select(p => new { p.Codigo, p.Seccion,
            Delegable = p.Delegable && actor.TienePermiso(p.Codigo) }), sedeId = sede };
    }

    public async Task<int> Guardar(int? id, GuardarRol input, CancellationToken ct)
    {
        Require("roles.gestionar");
        var sede = Sede();
        var nombre = input.Nombre?.Trim() ?? "";
        if (nombre.Length is < 2 or > 50 || input.Descripcion?.Length > 200 || input.Permisos is null || input.Permisos.Length > 100)
            throw new ConflictException("Revisa el nombre, descripción y permisos del rol.");
        var permisos = input.Permisos.Distinct().Order().ToArray();
        var validos = await db.Permisos.Where(p => p.Delegable).Select(p => p.Codigo).ToListAsync(ct);
        if (permisos.Any(p => !validos.Contains(p) || !actor.TienePermiso(p)))
            throw new ForbiddenAccessException("No puede conceder permisos fuera de su alcance delegable.");
        foreach (var p in permisos)
        {
            var ver = p.Split('.')[0] + ".ver";
            if (!permisos.Contains(ver)) throw new ConflictException("Las acciones requieren permiso de consulta de su sección.");
        }
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var rol = id.HasValue ? await db.Roles.Include(r => r.Permisos).SingleOrDefaultAsync(r => r.Id == id && r.SedeId == sede, ct)
            ?? throw new NotFoundException("Rol", id.Value) : new Rol { Codigo = "L" + Guid.NewGuid().ToString("N")[..28], Protegido = false, SedeId = sede };
        if (rol.Protegido || !rol.Activo) throw new ForbiddenAccessException("El rol está protegido o desactivado.");
        if (id.HasValue && rol.Version != input.Version) throw new ConflictException("El rol cambió. Actualiza antes de guardar.");
        if (id.HasValue && await db.UsuarioRoles.AnyAsync(x => x.RolId == id && x.UsuarioId == actor.UsuarioId, ct))
            throw new ForbiddenAccessException("No puede modificar un rol que tiene asignado.");
        if (await db.Roles.AnyAsync(r => r.SedeId == sede && r.Nombre == nombre && r.Id != rol.Id, ct))
            throw new ConflictException("Ya existe un rol con ese nombre en la sede.");
        var antes = Snapshot(rol);
        rol.Nombre = nombre; rol.Descripcion = input.Descripcion?.Trim(); rol.Version = Guid.NewGuid();
        foreach (var removed in rol.Permisos.Where(p => !permisos.Contains(p.PermisoCodigo)).ToArray())
        { db.RolPermisos.Remove(removed); rol.Permisos.Remove(removed); }
        foreach (var p in permisos.Where(p => !rol.Permisos.Any(rp => rp.PermisoCodigo == p)))
            rol.Permisos.Add(new RolPermiso { PermisoCodigo = p });
        if (!id.HasValue) db.Roles.Add(rol);
        await db.SaveChangesAsync(ct);
        Audit("Rol", rol.Id, antes, Snapshot(rol));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return rol.Id;
    }

    public async Task Desactivar(int id, Guid version, CancellationToken ct)
    {
        Require("roles.gestionar");
        var sede = Sede();
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var rol = await db.Roles.SingleOrDefaultAsync(r => r.Id == id && r.SedeId == sede, ct) ?? throw new NotFoundException("Rol", id);
        if (rol.Protegido) throw new ForbiddenAccessException("El rol está protegido.");
        if (rol.Version != version) throw new ConflictException("El rol cambió. Actualiza antes de continuar.");
        if (await db.UsuarioRoles.AnyAsync(x => x.RolId == id, ct)) throw new ConflictException("Reasigna los usuarios antes de desactivar este rol.");
        var antes = Snapshot(rol); rol.Activo = false; rol.Version = Guid.NewGuid();
        Audit("Rol", id, antes, Snapshot(rol));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<object> Personal(string? buscar, int pagina, CancellationToken ct)
    {
        Require("usuarios.asignar");
        var sede = Sede();
        if (pagina < 1 || pagina > 1000000 || buscar?.Length > 100) throw new ConflictException("Búsqueda o página inválida.");
        var query = db.Usuarios.AsNoTracking().Where(u => u.SedeId == sede && u.Persona.Cliente == null && !u.Roles.Any(r => r.Rol.Codigo == "CLIENTE"));
        if (!string.IsNullOrWhiteSpace(buscar)) query = query.Where(u => u.Correo.Contains(buscar));
        return new { total = await query.CountAsync(ct), items = await query.OrderBy(u => u.Correo).Skip((pagina - 1) * 20).Take(20)
            .Select(u => new { u.Id, u.Correo, u.Activo, Roles = u.Roles.Select(x => x.RolId).ToArray(),
                Protegido = u.Id == actor.UsuarioId || u.Roles.Any(x => x.Rol.Codigo == "ADMINISTRADOR" || x.Rol.Codigo == "SUPERADMINISTRADOR") }).ToListAsync(ct) };
    }

    public async Task Asignar(int usuarioId, AsignarRoles input, CancellationToken ct)
    {
        Require("usuarios.asignar");
        var sede = Sede();
        if (actor.UsuarioId == usuarioId) throw new ForbiddenAccessException("No puede modificar sus propios roles.");
        if (input.Roles is null || input.RolesAnteriores is null || input.Roles.Length is < 1 or > 20)
            throw new ConflictException("Selecciona entre uno y veinte roles.");
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await db.Usuarios.Include(u => u.Roles).ThenInclude(x => x.Rol).SingleOrDefaultAsync(u => u.Id == usuarioId && u.SedeId == sede && u.Persona.Cliente == null && !u.Roles.Any(r => r.Rol.Codigo == "CLIENTE"), ct)
            ?? throw new NotFoundException("Personal", usuarioId);
        if (!user.Activo || user.Roles.Any(x => x.Rol.Codigo is "ADMINISTRADOR" or "SUPERADMINISTRADOR"))
            throw new ForbiddenAccessException("La cuenta está protegida o desactivada.");
        var antes = user.Roles.Select(x => x.RolId).Order().ToArray();
        if (!antes.SequenceEqual(input.RolesAnteriores.Distinct().Order())) throw new ConflictException("Los roles cambiaron. Actualiza la lista.");
        var ids = input.Roles.Distinct().ToArray();
        var roles = await db.Roles.Include(r => r.Permisos).ThenInclude(p => p.Permiso)
            .Where(r => ids.Contains(r.Id) && r.Activo && (r.SedeId == sede || r.SedeId == null)).ToListAsync(ct);
        if (roles.Count != ids.Length || roles.Any(r => r.Codigo is "CLIENTE" or "ADMINISTRADOR" or "SUPERADMINISTRADOR" ||
            r.Permisos.Count == 0 || r.Permisos.Any(p => !p.Permiso.Delegable || !actor.TienePermiso(p.PermisoCodigo))))
            throw new ForbiddenAccessException("Uno de los roles no puede asignarse en esta sede o excede sus permisos delegables.");
        foreach (var removed in user.Roles.Where(x => !ids.Contains(x.RolId)).ToArray()) db.UsuarioRoles.Remove(removed);
        foreach (var id in ids.Where(id => !antes.Contains(id))) user.Roles.Add(new UsuarioRol { RolId = id, FechaAsignacion = DateTime.UtcNow });
        Audit("UsuarioRoles", usuarioId, JsonSerializer.Serialize(antes), JsonSerializer.Serialize(ids));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<object> RolesPersonal(int id, CancellationToken ct)
    {
        Require("usuarios.asignar");
        var sede = Sede();
        var user = await db.Usuarios.AsNoTracking().Where(u => u.Id == id && u.SedeId == sede && u.Persona.Cliente == null && !u.Roles.Any(r => r.Rol.Codigo == "CLIENTE"))
            .Select(u => new { u.Id, u.Activo, Roles = u.Roles.Select(r => r.RolId).ToArray(), Protegido = u.Id == actor.UsuarioId || u.Roles.Any(r => r.Rol.Codigo == "ADMINISTRADOR" || r.Rol.Codigo == "SUPERADMINISTRADOR") }).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Personal", id);
        return user;
    }

    public async Task<int> CrearPersonalAsync(CrearPersonal input, IPasswordHasher hasher, CancellationToken ct)
    {
        Require("usuarios.asignar");
        if (!actor.TienePermiso("usuarios.editar")) throw new ForbiddenAccessException("No tiene permiso para crear personal.");
        var sede = Sede();
        var validation = new CasaAndina.Application.Auth.Commands.RegisterClientCommandValidator().Validate(
            new CasaAndina.Application.Auth.Commands.RegisterClientCommand("N", input.TipoDocumento, input.NumeroDocumento, input.NombreCompleto, null, input.Email, input.Telefono, input.Password));
        if (!validation.IsValid) throw new CasaAndina.Application.Common.Exceptions.ValidationException(validation.Errors);
        if (input.Roles is null || input.Roles.Length is < 1 or > 20) throw new ConflictException("Selecciona entre uno y veinte roles.");
        var ids = input.Roles.Distinct().ToArray();
        var email = input.Email.Trim().ToLowerInvariant();
        var numero = input.NumeroDocumento.Trim().ToUpperInvariant();
        var tipo = input.TipoDocumento.Trim().ToUpperInvariant();
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var roles = await db.Roles.Include(r => r.Permisos).ThenInclude(p => p.Permiso)
            .Where(r => ids.Contains(r.Id) && r.Activo && (r.SedeId == sede || r.SedeId == null)).ToListAsync(ct);
        if (roles.Count != ids.Length || roles.Any(r => r.Codigo is "CLIENTE" or "ADMINISTRADOR" or "SUPERADMINISTRADOR" ||
            r.Permisos.Count == 0 || r.Permisos.Any(p => !p.Permiso.Delegable || !actor.TienePermiso(p.PermisoCodigo))))
            throw new ForbiddenAccessException("Uno de los roles no puede asignarse en esta sede o excede sus permisos delegables.");
        var documento = await db.TiposDocumento.SingleOrDefaultAsync(x => x.Codigo == tipo && x.Activo && (x.TipoPersonaPermitida == "N" || x.TipoPersonaPermitida == "A"), ct)
            ?? throw new ConflictException("El tipo de documento no está disponible.");
        if (await db.Usuarios.AnyAsync(u => u.Correo == email, ct) || await db.PersonaDocumentos.AnyAsync(d => d.TipoDocumentoId == documento.Id && d.Numero == numero, ct))
            throw new ConflictException("Ya existe una cuenta o persona registrada con ese correo o documento.");
        var persona = new Persona { TipoPersona = "N", Estado = "Activo", FechaRegistro = DateTime.UtcNow,
            Natural = new PersonaNatural { Nombres = string.Join(' ', input.NombreCompleto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)) } };
        persona.Documentos.Add(new PersonaDocumento { TipoDocumentoId = documento.Id, Numero = numero, PaisEmision = "Perú", EsPrincipal = true });
        if (!string.IsNullOrWhiteSpace(input.Telefono)) persona.Contactos.Add(new PersonaContacto { TipoContacto = "Telefono", Valor = input.Telefono.Trim(), EsPrincipal = true });
        var user = new Usuario { Correo = email, PasswordHash = hasher.Hash(input.Password), Persona = persona, SedeId = sede, Activo = true, FechaCreacion = DateTime.UtcNow,
            Roles = ids.Select(id => new UsuarioRol { RolId = id, FechaAsignacion = DateTime.UtcNow }).ToList() };
        db.Usuarios.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("No se pudo crear la cuenta. Verifica si el correo o documento ya están registrados."); }
        db.Auditoria.Add(new Auditoria { UsuarioId = actor.UsuarioId, SedeId = sede, FechaUtc = DateTime.UtcNow, Accion = "CREATE", Entidad = "Usuario", EntidadId = user.Id.ToString(), Despues = JsonSerializer.Serialize(new { user.Correo, user.SedeId, Roles = ids }) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return user.Id;
    }

    private static string Snapshot(Rol rol) => JsonSerializer.Serialize(new { rol.Nombre, rol.Descripcion, rol.Activo, Permisos = rol.Permisos.Select(p => p.PermisoCodigo).Order() });
    private void Audit(string entidad, int id, string antes, string despues) => db.Auditoria.Add(new Auditoria {
        UsuarioId = actor.UsuarioId, SedeId = actor.SedeId, FechaUtc = DateTime.UtcNow, Accion = "UPDATE",
        Entidad = entidad, EntidadId = id.ToString(), Antes = antes, Despues = despues });
}
