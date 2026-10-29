using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Perfil;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class CorreosPerfilTests
{
    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAANDINA_TEST_SQLSERVER"))) Skip = "Defina CASAANDINA_TEST_SQLSERVER para las pruebas SQL aisladas."; }
    }
    private sealed record User(int? UsuarioId = 1, int? ClienteId = 1) : ICurrentUserService { public int? SedeId => null; public RolUsuario? Rol => RolUsuario.Cliente; }
    private sealed class Hasher : IPasswordHasher { public string Hash(string password) => password; public bool Verify(string password, string hash) => password == hash; public void VerifyAgainstDummyHash(string password) { } }
    private static async Task<ApplicationDbContext> Seed()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var person = new Persona { Id = 1, TipoPersona = "N", Cliente = new Cliente { Id = 1, Estado = "Activo" } };
        person.Contactos.Add(new PersonaContacto { Id = 1, TipoContacto = "Correo", Valor = "principal@test.example", EsPrincipal = true });
        db.Usuarios.Add(new Usuario { Id = 1, Correo = "acceso@test.example", PasswordHash = "clave", Persona = person, Activo = true });
        var other = new Persona { Id = 2, TipoPersona = "N", Cliente = new Cliente { Id = 2, Estado = "Activo" } };
        other.Contactos.Add(new PersonaContacto { Id = 2, TipoContacto = "Correo", Valor = "ajeno@test.example", EsPrincipal = true });
        db.Usuarios.Add(new Usuario { Id = 2, Correo = "otro@test.example", PasswordHash = "otro", Persona = other, Activo = true });
        await db.SaveChangesAsync(); return db;
    }

    [Fact]
    public async Task Agrega_y_cambia_principal_sin_modificar_contactos_ajenos()
    {
        await using var db = await Seed(); var service = new CorreosPerfilService(db, new User());
        await service.Agregar(" NUEVO@test.example ", default);
        await service.Principal("nuevo@test.example", default);
        var own = await db.PersonaContactos.Where(c => c.PersonaId == 1 && c.TipoContacto == "Correo").ToListAsync();
        own.Should().ContainSingle(c => c.EsPrincipal && c.Valor == "nuevo@test.example");
        (await db.PersonaContactos.SingleAsync(c => c.PersonaId == 2)).EsPrincipal.Should().BeTrue();
        var foreign = () => service.Principal("ajeno@test.example", default);
        await foreign.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Rechaza_colisiones_y_cambio_de_acceso_sin_contrasena()
    {
        await using var db = await Seed(); var service = new CorreosPerfilService(db, new User());
        var duplicate = () => service.Agregar("ajeno@test.example", default);
        await duplicate.Should().ThrowAsync<ConflictException>();
        var wrong = () => service.Acceso(new("nuevo@test.example", "incorrecta"), new Hasher(), default);
        await wrong.Should().ThrowAsync<ConflictException>();
        (await db.Usuarios.FindAsync(1))!.Correo.Should().Be("acceso@test.example");
        await service.Acceso(new("nuevo@test.example", "clave"), new Hasher(), default);
        (await db.Usuarios.FindAsync(1))!.Correo.Should().Be("nuevo@test.example");
        db.Auditoria.Should().Contain(x => x.Accion == "UPDATE" && x.Despues!.Contains("CAMBIAR_CORREO_ACCESO") && !x.Despues.Contains("clave"));
    }

    [Fact]
    public async Task Rechaza_perfil_de_otro_cliente_o_sin_identidad()
    {
        await using var db = await Seed();
        var mismatch = () => new CorreosPerfilService(db, new User(1, 2)).Agregar("nuevo@test.example", default);
        await mismatch.Should().ThrowAsync<ForbiddenAccessException>();
        var anonymous = () => new CorreosPerfilService(db, new User(null, null)).Agregar("nuevo@test.example", default);
        await anonymous.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [SqlServerFact]
    public async Task SqlServer_respeta_restriccion_de_auditoria_al_guardar_correos()
    {
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CASAANDINA_TEST_SQLSERVER"))
        { InitialCatalog = $"CasaAndinaTestCorreos_{Guid.NewGuid():N}" };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection.ConnectionString).Options);
        var created = false;
        try
        {
            created = await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Auditoria ADD CONSTRAINT CK_Test_Accion CHECK (Accion IN (N'INSERT',N'UPDATE',N'DELETE',N'LOGIN',N'LOGOUT',N'OTRA'))");
            var person = new Persona { TipoPersona = "N", Estado = "Activo", Natural = new PersonaNatural { Nombres = "Prueba" }, Cliente = new Cliente { Estado = "Activo" } };
            var account = new Usuario { Correo = "acceso@test.example", PasswordHash = "clave", Persona = person, Activo = true };
            person.Contactos.Add(new PersonaContacto { TipoContacto = "Correo", Valor = "principal@test.example", EsPrincipal = true });
            db.Usuarios.Add(account); await db.SaveChangesAsync();
            var service = new CorreosPerfilService(db, new User(account.Id, person.Cliente.Id));
            await service.Agregar("segundo@test.example", default);
            await service.Principal("segundo@test.example", default);
            await service.Acceso(new("nuevo@test.example", "clave"), new Hasher(), default);
            (await db.Auditoria.CountAsync()).Should().Be(3);
            (await db.PersonaContactos.SingleAsync(c => c.EsPrincipal && c.TipoContacto == "Correo")).Valor.Should().Be("segundo@test.example");
        }
        finally { if (created) await db.Database.EnsureDeletedAsync(); }
    }
}
