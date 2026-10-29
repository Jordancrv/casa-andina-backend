using System.Data;
using System.Security.Claims;
using CasaAndina.Application.Accesos;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Identity;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class RolesPermisosTests
{
    private sealed record Actor(int? SedeId = 1, int? UsuarioId = 1, int? ClienteId = null) : ICurrentUserService
    { public RolUsuario? Rol => RolUsuario.Administrador; }
    // InMemory cannot exercise transaction isolation; this adapter tests validation/persistence only.
    private sealed class Db : ApplicationDbContext, IApplicationDbContext
    {
        public Db() : base(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { }
        public new Task<IApplicationTransaction> BeginTransactionAsync(IsolationLevel level, CancellationToken ct = default) => Task.FromResult<IApplicationTransaction>(new Tx());
        private sealed class Tx : IApplicationTransaction { public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask; public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }
    private static async Task<Db> Seed()
    {
        var db = new Db();
        db.Permisos.AddRange(new Permiso { Codigo = "reportes.ver", Seccion = "reportes", Delegable = true }, new Permiso { Codigo = "reportes.analizar", Seccion = "reportes", Delegable = true }, new Permiso { Codigo = "roles.gestionar", Seccion = "roles" });
        db.Roles.AddRange(new Rol { Id = 1, Codigo = "ADMINISTRADOR", Nombre = "Administrador" },
            new Rol { Id = 2, Codigo = "LOCAL_A", Nombre = "Consulta", Protegido = false, SedeId = 1, Permisos = [new() { PermisoCodigo = "reportes.ver" }] },
            new Rol { Id = 3, Codigo = "LOCAL_B", Nombre = "Consulta otra sede", Protegido = false, SedeId = 2, Permisos = [new() { PermisoCodigo = "reportes.ver" }] });
        db.Usuarios.AddRange(Person(1, 1, 1), Person(2, 1, 2), Person(3, 2, 3));
        await db.SaveChangesAsync(); return db;
    }
    private static Usuario Person(int id, int sede, int role) => new() { Id = id, SedeId = sede, Correo = $"u{id}@test.example", PasswordHash = "hash", PersonaId = id, Persona = new() { Id = id, TipoPersona = "N" }, Roles = [new() { RolId = role }] };
    private sealed class Hasher : IPasswordHasher { public string Hash(string password) => "hashed:" + password; public bool Verify(string password, string hash) => hash == Hash(password); public void VerifyAgainstDummyHash(string password) { } }

    [Fact]
    public async Task Crear_personal_asigna_sede_roles_y_guarda_hash_y_auditoria()
    {
        await using var db = await Seed();
        db.TiposDocumento.Add(new TipoDocumento { Id = 1, Codigo = "DNI", Nombre = "DNI", TipoPersonaPermitida = "N", LongitudMinima = 8, LongitudMaxima = 8 });
        await db.SaveChangesAsync();
        var service = new RolesService(db, new Actor());
        var id = await service.CrearPersonalAsync(new("Ana Torres", "DNI", "12345678", "ANA@test.example", null, "ClaveSegura1!", [2]), new Hasher(), default);
        var created = await db.Usuarios.Include(u => u.Roles).Include(u => u.Persona).ThenInclude(p => p.Documentos).SingleAsync(u => u.Id == id);
        created.SedeId.Should().Be(1); created.Correo.Should().Be("ana@test.example");
        created.PasswordHash.Should().Be("hashed:ClaveSegura1!"); created.Roles.Should().ContainSingle(r => r.RolId == 2);
        created.Persona.Cliente.Should().BeNull(); created.Persona.Documentos.Single().Numero.Should().Be("12345678");
        (await db.Auditoria.SingleAsync(x => x.Entidad == "Usuario" && x.EntidadId == id.ToString())).Despues.Should().NotContain("ClaveSegura1!");
    }

    [Fact]
    public async Task Crear_personal_rechaza_rol_de_otra_sede_y_contrasena_invalida()
    {
        await using var db = await Seed(); var service = new RolesService(db, new Actor());
        var invalid = () => service.CrearPersonalAsync(new("Ana Torres", "DNI", "12345678", "ana@test.example", null, "clave", [2]), new Hasher(), default);
        await invalid.Should().ThrowAsync<CasaAndina.Application.Common.Exceptions.ValidationException>();
        var foreign = () => service.CrearPersonalAsync(new("Ana Torres", "DNI", "12345678", "ana@test.example", null, "ClaveSegura1!", [3]), new Hasher(), default);
        await foreign.Should().ThrowAsync<ForbiddenAccessException>();
        (await db.Usuarios.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Crea_rol_local_y_audita_sin_modificar_plantillas()
    {
        await using var db = await Seed(); var service = new RolesService(db, new Actor());
        var id = await service.Guardar(null, new("Analista", "Consulta reportes", ["reportes.ver"], null), default);
        var role = await db.Roles.FindAsync(id);
        role!.SedeId.Should().Be(1); role.Protegido.Should().BeFalse();
        db.Auditoria.Should().ContainSingle(x => x.Entidad == "Rol" && x.EntidadId == id.ToString());
        (await db.Roles.FindAsync(1))!.Nombre.Should().Be("Administrador");
    }

    [Theory]
    [InlineData("roles.gestionar")]
    [InlineData("sedes.ver")]
    [InlineData("inventado.ver")]
    [InlineData("reportes.analizar")]
    public async Task Rechaza_permisos_no_delegables_inexistentes_o_sin_consulta(string permission)
    {
        await using var db = await Seed(); var service = new RolesService(db, new Actor());
        var action = () => service.Guardar(null, new("Inválido", null, [permission], null), default);
        await action.Should().ThrowAsync<Exception>();
        (await db.Roles.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Rechaza_edicion_ajena_protegida_propia_y_obsoleta()
    {
        await using var db = await Seed(); var service = new RolesService(db, new Actor());
        foreach (var id in new[] { 1, 3 })
        {
            var role = await db.Roles.FindAsync(id);
            var action = () => service.Guardar(id, new("Cambio", null, ["reportes.ver"], role!.Version), default);
            await action.Should().ThrowAsync<NotFoundException>();
        }
        var stale = () => service.Guardar(2, new("Cambio", null, ["reportes.ver"], Guid.NewGuid()), default);
        await stale.Should().ThrowAsync<ConflictException>();
        db.UsuarioRoles.Add(new() { UsuarioId = 1, RolId = 2 }); await db.SaveChangesAsync();
        var own = () => service.Guardar(2, new("Cambio", null, ["reportes.ver"], db.Roles.Local.Single(r => r.Id == 2).Version), default);
        await own.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Asigna_varios_roles_y_rechaza_perdida_de_actualizaciones()
    {
        await using var db = await Seed(); var service = new RolesService(db, new Actor());
        var id = await service.Guardar(null, new("Análisis", null, ["reportes.ver", "reportes.analizar"], null), default);
        await service.Asignar(2, new([2, id], [2]), default);
        (await db.UsuarioRoles.Where(x => x.UsuarioId == 2).CountAsync()).Should().Be(2);
        var stale = () => service.Asignar(2, new([2], [2]), default);
        await stale.Should().ThrowAsync<ConflictException>();
        db.Auditoria.Should().Contain(x => x.Entidad == "UsuarioRoles");
    }

    [Fact]
    public async Task Asignacion_impide_otra_sede_admin_autoelevacion_y_clientes()
    {
        await using var db = await Seed(); var service = new RolesService(db, new Actor());
        var cross = () => service.Asignar(3, new([2], [3]), default);
        await cross.Should().ThrowAsync<NotFoundException>();
        var foreignRole = () => service.Asignar(2, new([3], [2]), default);
        await foreignRole.Should().ThrowAsync<ForbiddenAccessException>();
        var admin = () => service.Asignar(2, new([1], [2]), default);
        await admin.Should().ThrowAsync<ForbiddenAccessException>();
        var own = () => service.Asignar(1, new([2], [1]), default);
        await own.Should().ThrowAsync<ForbiddenAccessException>();
        var user = await db.Usuarios.Include(x => x.Persona).SingleAsync(x => x.Id == 2);
        user.Persona.Cliente = new Cliente { Id = 2, PersonaId = 2 }; db.Clientes.Add(user.Persona.Cliente); await db.SaveChangesAsync();
        var client = () => service.Asignar(2, new([2], [2]), default);
        await client.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task No_desactiva_roles_asignados()
    {
        await using var db = await Seed(); var service = new RolesService(db, new Actor());
        var action = () => service.Desactivar(2, db.Roles.Local.Single(r => r.Id == 2).Version, default);
        await action.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public void Permisos_efectivos_no_dependen_del_primer_rol_y_no_tienen_fallback_por_nombre()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Role, "Administrador"), new("permiso", "reportes.ver")], "test"));
        var user = new CurrentUserService(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });
        user.TienePermiso("reportes.ver").Should().BeTrue();
        user.TienePermiso("reportes.analizar").Should().BeFalse();
    }
}
