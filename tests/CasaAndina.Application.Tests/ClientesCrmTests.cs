using CasaAndina.Application.Clientes.Commands;
using CasaAndina.Application.Clientes.Queries;
using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ClientesCrmTests
{
    [Theory]
    [InlineData(0, "Ocasional")][InlineData(1, "Ocasional")][InlineData(2, "Frecuente")]
    [InlineData(4, "Frecuente")][InlineData(5, "VIP")][InlineData(9, "VIP")]
    public async Task Categoria_depende_solo_de_estadias_completadas_del_alcance(int cantidad, string categoria)
    {
        await using var db = Context();
        var c = Client(1, 1); db.Clientes.Add(c);
        for (var i = 0; i < cantidad; i++) AddReservation(db, c, 1, "Completada");
        AddReservation(db, c, 1, "Cancelada");
        AddReservation(db, c, 1, "Reservado");
        for (var i = 0; i < 6; i++) AddReservation(db, c, 2, "Completada");
        await db.SaveChangesAsync();
        var result = await Query(db).Handle(new GetClientesAdminQuery(Categoria: categoria), default);
        var item = result.Items.Should().ContainSingle().Subject;
        item.Categoria.Should().Be(categoria); item.EstadiasCompletadas.Should().Be(cantidad);
        item.Historial.Should().OnlyContain(r => r.Sede == "Sede 1");
    }

    [Fact]
    public async Task Kpis_cuentan_clientes_sin_duplicados_y_mes_en_horario_Peru()
    {
        await using var db = Context();
        var hoy = PeruBusinessTime.Today;
        var inicio = PeruBusinessTime.UtcStartOfDay(new DateTime(hoy.Year, hoy.Month, 1));
        var nuevo = Client(1, 1); nuevo.FechaRegistro = inicio;
        var previo = Client(2, 1); previo.FechaRegistro = inicio.AddTicks(-1);
        var futuro = Client(3, 1); futuro.FechaRegistro = PeruBusinessTime.UtcStartOfDay(new DateTime(hoy.Year, hoy.Month, 1).AddMonths(1));
        var otraSede = Client(4, 2);
        db.Clientes.AddRange(nuevo, previo, futuro, otraSede);
        AddReservation(db, nuevo, 1, "Reservado"); AddReservation(db, nuevo, 1, "Pendiente");
        var vencida = AddReservation(db, previo, 1, "Reservado"); vencida.Habitaciones.Single().FechaCheckOut = hoy.AddDays(-1);
        AddReservation(db, futuro, 1, "Cancelada");
        await db.SaveChangesAsync();
        var result = await Query(db).Handle(new(), default);
        result.Kpis.TotalClientes.Should().Be(3);
        result.Kpis.ConReservasActivas.Should().Be(1);
        result.Kpis.NuevosEsteMes.Should().Be(1);
        var activas = await Query(db).Handle(new(Actividad: "ConReservasActivas"), default);
        activas.Items.Should().ContainSingle().Which.ReservasActivas.Should().Be(2);
        activas.Kpis.TotalClientes.Should().Be(1);
        var nuevos = await Query(db).Handle(new(Actividad: "NuevosMes"), default);
        nuevos.Items.Should().ContainSingle().Which.ClienteId.Should().Be(nuevo.Id);
    }

    [Fact]
    public async Task Estadia_en_curso_con_salida_prevista_vencida_sigue_activa_hasta_checkout()
    {
        await using var db = Context();
        var c = Client(1, 1); db.Clientes.Add(c);
        var r = AddReservation(db, c, 1, "Reservado");
        r.Habitaciones.Single().FechaCheckOut = PeruBusinessTime.Today.AddDays(-1);
        r.Habitaciones.Single().FechaCheckInReal = DateTime.UtcNow.AddDays(-3);
        await db.SaveChangesAsync();
        (await Query(db).Handle(new(), default)).Kpis.ConReservasActivas.Should().Be(1);
        r.Habitaciones.Single().FechaCheckOutReal = DateTime.UtcNow;
        await db.SaveChangesAsync();
        (await Query(db).Handle(new(), default)).Kpis.ConReservasActivas.Should().Be(0);
    }

    [Fact]
    public async Task Filtra_documento_nombre_completo_estado_sede_y_pagina_sin_perder_kpis()
    {
        await using var db = Context();
        var tipo = DocumentType();
        var ana = Client(1, 1); ana.Persona.Natural!.Nombres = "Ana"; ana.Persona.Natural.Apellidos = "Torres";
        ana.Persona.Documentos.Add(new() { TipoDocumento = tipo, TipoDocumentoId = tipo.Id, Numero = "74582136", EsPrincipal = true });
        var inactivo = Client(2, 1); inactivo.Estado = "Inactivo";
        db.Clientes.AddRange(ana, inactivo, Client(3, 1), Client(4, 2)); await db.SaveChangesAsync();
        (await Query(db).Handle(new("74582136"), default)).Items.Should().ContainSingle().Which.ClienteId.Should().Be(ana.Id);
        (await Query(db).Handle(new("Ana Torres"), default)).Items.Should().ContainSingle();
        var soloInactivos = await Query(db).Handle(new(Estado: "Inactivo"), default);
        soloInactivos.Kpis.TotalClientes.Should().Be(1);
        var page = await Query(db).Handle(new(PageNumber: 999, PageSize: 2), default);
        page.PageNumber.Should().Be(2); page.Items.Should().ContainSingle(); page.Kpis.TotalClientes.Should().Be(3);
        var global = new GetClientesAdminQueryHandler(db, new User(RolUsuario.Superadministrador, null));
        (await global.Handle(new(SedeId: 2), default)).Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Rechaza_filtros_y_acceso_fuera_de_sede_incluso_con_id_conocido()
    {
        await using var db = Context();
        db.Clientes.Add(Client(1, 2)); await db.SaveChangesAsync();
        var forbidden = () => Query(db).Handle(new(SedeId: 2), default);
        await forbidden.Should().ThrowAsync<ForbiddenAccessException>();
        var edit = () => new GuardarClienteCommandHandler(db, new User()).Handle(new ActualizarClienteCommand(1, Datos with { SedeRegistroId = 2 }), default);
        await edit.Should().ThrowAsync<NotFoundException>();
        var validator = new GetClientesAdminQueryValidator();
        validator.Validate(new GetClientesAdminQuery(Estado: "Otro")).IsValid.Should().BeFalse();
        validator.Validate(new GetClientesAdminQuery(Categoria: "Premium")).IsValid.Should().BeFalse();
        validator.Validate(new GetClientesAdminQuery(Actividad: "Otra")).IsValid.Should().BeFalse();
        validator.Validate(new GetClientesAdminQuery(PageNumber: int.MaxValue)).IsValid.Should().BeFalse();
        foreach (var user in new[] { new User(RolUsuario.Recepcion, 1), new User(RolUsuario.Administrador, null) })
        {
            var act = () => new GetClientesAdminQueryHandler(db, user).Handle(new(), default);
            await act.Should().ThrowAsync<ForbiddenAccessException>();
        }
    }

    [Fact]
    public async Task Alta_manual_es_visible_sin_reservas_y_no_crea_cuenta_de_acceso()
    {
        await using var db = Context(); await SeedCatalog(db);
        var handler = new GuardarClienteCommandHandler(db, new User());
        var id = await handler.Handle(new CrearClienteCommand(Datos with { Nombre = "  Ana   María ", CorreoNotificacion = " ANA@EXAMPLE.TEST " }), default);
        var result = await Query(db).Handle(new(), default);
        var item = result.Items.Should().ContainSingle().Subject;
        item.ClienteId.Should().Be(id); item.Nombre.Should().Be("Ana María Torres");
        item.Categoria.Should().Be("Ocasional"); item.TotalReservas.Should().Be(0);
        item.SedeRegistroId.Should().Be(1); item.SedeReferencia.Should().Be("Sede 1");
        item.CorreoNotificacion.Should().Be("ana@example.test");
        db.Usuarios.Should().BeEmpty();
    }

    [Fact]
    public async Task Edicion_actualiza_perfil_y_estado_sin_cambiar_correo_de_acceso_ni_sede_de_origen()
    {
        await using var db = Context(); await SeedCatalog(db);
        var handler = new GuardarClienteCommandHandler(db, new User());
        var id = await handler.Handle(new CrearClienteCommand(Datos), default);
        var client = await db.Clientes.Include(c => c.Persona).SingleAsync();
        db.Usuarios.Add(new Usuario { Persona = client.Persona, Correo = "acceso@example.test", PasswordHash = "hash-test" });
        await db.SaveChangesAsync();
        await handler.Handle(new ActualizarClienteCommand(id, Datos with { Nombre = "Ana Editada", CorreoNotificacion = "nuevo@example.test", Estado = "Inactivo", SedeRegistroId = 1 }), default);
        var item = (await Query(db).Handle(new(), default)).Items.Single();
        item.Nombre.Should().Be("Ana Editada Torres"); item.Estado.Should().Be("Inactivo");
        item.CorreoAcceso.Should().Be("acceso@example.test"); item.CorreoNotificacion.Should().Be("nuevo@example.test");
        var cambiarSede = () => handler.Handle(new ActualizarClienteCommand(id, Datos with { SedeRegistroId = 2 }), default);
        await cambiarSede.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Rechaza_documento_correo_duplicado_y_alta_en_sede_ajena()
    {
        await using var db = Context(); await SeedCatalog(db);
        var handler = new GuardarClienteCommandHandler(db, new User());
        await handler.Handle(new CrearClienteCommand(Datos), default);
        var duplicateDoc = () => handler.Handle(new CrearClienteCommand(Datos with { CorreoNotificacion = "otro@example.test" }), default);
        await duplicateDoc.Should().ThrowAsync<ConflictException>();
        var duplicateEmail = () => handler.Handle(new CrearClienteCommand(Datos with { NumeroDocumento = "87654321", CorreoNotificacion = "ANA@EXAMPLE.TEST" }), default);
        await duplicateEmail.Should().ThrowAsync<ConflictException>();
        var anotherSite = () => handler.Handle(new CrearClienteCommand(Datos with { SedeRegistroId = 2 }), default);
        await anotherSite.Should().ThrowAsync<ForbiddenAccessException>();
        db.Clientes.Should().ContainSingle();
    }

    [Fact]
    public async Task Edicion_no_duplica_documentos_o_contactos_adicionales_de_la_misma_persona()
    {
        await using var db = Context(); await SeedCatalog(db);
        var handler = new GuardarClienteCommandHandler(db, new User());
        var id = await handler.Handle(new CrearClienteCommand(Datos), default);
        var persona = (await db.Clientes.Include(c => c.Persona).SingleAsync()).Persona;
        persona.Documentos.Add(new PersonaDocumento { TipoDocumentoId = 1, Numero = "87654321", EsPrincipal = false });
        persona.Contactos.Add(new PersonaContacto { TipoContacto = "Correo", Valor = "adicional@example.test", EsPrincipal = false });
        await db.SaveChangesAsync();
        var document = () => handler.Handle(new ActualizarClienteCommand(id, Datos with { NumeroDocumento = "87654321", SedeRegistroId = 1 }), default);
        await document.Should().ThrowAsync<ConflictException>();
        var email = () => handler.Handle(new ActualizarClienteCommand(id, Datos with { CorreoNotificacion = "adicional@example.test", SedeRegistroId = 1 }), default);
        await email.Should().ThrowAsync<ConflictException>();
        persona.Documentos.Count.Should().Be(2);
    }

    [Fact]
    public async Task Catalogos_respetan_alcance_y_alta_global_exige_sede()
    {
        await using var db = Context(); await SeedCatalog(db);
        var site = new Sede { Id = 2, Nombre = "Otra", Codigo = "OTR", Ciudad = "Lima", Categoria = "Standard", Region = "Costa" };
        db.Sedes.Add(site); await db.SaveChangesAsync();
        var local = await new GetCatalogosCrmQueryHandler(db, new User()).Handle(new(), default);
        local.PuedeElegirSede.Should().BeFalse(); local.Sedes.Should().ContainSingle().Which.Id.Should().Be(1);
        var globalUser = new User(RolUsuario.Superadministrador, null);
        var global = await new GetCatalogosCrmQueryHandler(db, globalUser).Handle(new(), default);
        global.PuedeElegirSede.Should().BeTrue(); global.Sedes.Should().HaveCount(2);
        var noSite = () => new GuardarClienteCommandHandler(db, globalUser).Handle(new CrearClienteCommand(Datos), default);
        await noSite.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Valida_identidad_juridica_documento_activo_y_formulario()
    {
        await using var db = Context(); await SeedCatalog(db);
        db.TiposDocumento.Add(new() { Id = 2, Codigo = "RUC", Nombre = "RUC", TipoPersonaPermitida = "J", LongitudMinima = 11, LongitudMaxima = 11 });
        await db.SaveChangesAsync();
        var handler = new GuardarClienteCommandHandler(db, new User());
        var invalidType = () => handler.Handle(new CrearClienteCommand(Datos with { TipoPersona = "J" }), default);
        await invalidType.Should().ThrowAsync<ConflictException>();
        var invalidDoc = () => handler.Handle(new CrearClienteCommand(Datos with { NumeroDocumento = "ABCDEFGH" }), default);
        await invalidDoc.Should().ThrowAsync<ConflictException>();
        await handler.Handle(new CrearClienteCommand(Datos with { TipoPersona = "J", Nombre = "Empresa SAC", Apellidos = null, TipoDocumentoId = 2, NumeroDocumento = "20123456789" }), default);
        (await Query(db).Handle(new(), default)).Items.Should().ContainSingle().Which.TipoPersona.Should().Be("Jurídica");
        var validator = new ClienteCrmDatosValidator();
        foreach (var invalid in new[] { Datos with { Nombre = " " }, Datos with { Nombre = "Ana123" }, Datos with { Telefono = "abc" }, Datos with { Estado = "VIP" }, Datos with { NumeroDocumento = "12 34" } })
            validator.Validate(invalid).IsValid.Should().BeFalse();
        new CrearClienteCommandValidator().Validate(new CrearClienteCommand(null!)).IsValid.Should().BeFalse();
    }

    private static readonly ClienteCrmDatos Datos = new("N", "Ana", "Torres", null, 1, "12345678", "ana@example.test", "+51987654321");
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static GetClientesAdminQueryHandler Query(ApplicationDbContext db) => new(db, new User());
    private static TipoDocumento DocumentType() => new() { Id = 1, Codigo = "DNI", Nombre = "DNI", TipoPersonaPermitida = "N", LongitudMinima = 8, LongitudMaxima = 8 };
    private static async Task SeedCatalog(ApplicationDbContext db)
    {
        db.Sedes.Add(new() { Id = 1, Nombre = "Sede 1", Codigo = "TST", Ciudad = "Lima", Region = "Costa", Categoria = "Standard" });
        db.TiposDocumento.Add(DocumentType()); await db.SaveChangesAsync();
    }
    private static Cliente Client(int id, int sede) => new()
    {
        Id = id, PersonaId = id, SedeRegistroId = sede, FechaRegistro = DateTime.UtcNow,
        Persona = new Persona { Id = id, TipoPersona = "N", Natural = new PersonaNatural { PersonaId = id, Nombres = $"Cliente {id}" } }
    };
    private static Reserva AddReservation(ApplicationDbContext db, Cliente c, int sede, string estado)
    {
        var r = new Reserva { Cliente = c, ClienteId = c.Id, SedeId = sede, CodigoReserva = $"T{Guid.NewGuid().ToString("N")[..8]}", Estado = estado,
            Sede = db.Sedes.Local.FirstOrDefault(s => s.Id == sede) ?? new Sede { Id = sede, Nombre = $"Sede {sede}", Codigo = $"S{sede}", Region = "Costa", Ciudad = "Lima", Categoria = "Standard" },
            Habitaciones = { new ReservaHabitacion { FechaCheckIn = PeruBusinessTime.Today, FechaCheckOut = PeruBusinessTime.Today.AddDays(2) } } };
        db.Reservas.Add(r); return r;
    }
    private sealed class User(RolUsuario role = RolUsuario.Administrador, int? sedeId = 1) : ICurrentUserService
    {
        public int? UsuarioId => null; public int? ClienteId => null; public int? SedeId => sedeId; public RolUsuario? Rol => role;
    }
}
