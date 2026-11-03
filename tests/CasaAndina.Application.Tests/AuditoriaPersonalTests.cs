using System.Security.Claims;
using CasaAndina.Api.Controllers;
using CasaAndina.Api.Middleware;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Identity;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class AuditoriaPersonalTests
{
    private sealed record Actor(int? SedeId, RolUsuario? Rol = RolUsuario.Administrador) : ICurrentUserService
    {
        public int? UsuarioId => 1; public int? ClienteId => null;
    }
    private static Usuario Personal(int id, int sede) => new() { Id = id, SedeId = sede, Correo = $"{id}@example.test", PasswordHash = "secret", Persona = new Persona { Id = id, Natural = new PersonaNatural { Nombres = $"Persona {id}" } } };

    [Fact]
    public async Task Movimientos_automaticos_son_visibles_solo_en_su_sede_y_busqueda_admite_actor_nulo()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Auditoria.AddRange(new Auditoria { Id = 1, SedeId = 5, Accion = "UPDATE", Entidad = "ReservaHabitacion", EntidadId = "1", Despues = "{\"Movimiento\":\"CHECK_IN\",\"Origen\":\"AUTOMATICO\"}" },
            new Auditoria { Id = 2, SedeId = 6, Accion = "UPDATE", Entidad = "ReservaHabitacion", EntidadId = "2" });
        await db.SaveChangesAsync();
        var controller = new AuditoriaController(db, new Actor(5)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var response = (await controller.Get(buscar: "Sistema", ct: default)).Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        json.Should().Contain("Sistema").And.Contain("CheckIn");
        (await controller.Get(buscar: "nadie", ct: default)).Should().BeOfType<OkObjectResult>();
        var detail = (await controller.Detalle(1, default)).Should().BeOfType<OkObjectResult>().Subject;
        System.Text.Json.JsonSerializer.Serialize(detail.Value).Should().Contain("AUTOMATICO");
        (await controller.Detalle(2, default)).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Consulta_solo_administrador_sede_y_no_devuelve_datos_de_otra_sede()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = Personal(1, 5); var foreign = Personal(2, 6); var client = Personal(3, 5); client.Persona.Cliente = new Cliente();
        db.Usuarios.AddRange(staff, foreign, client);
        db.Auditoria.AddRange(new Auditoria { Id = 10, Usuario = staff, SedeId = 5, Accion = "UPDATE", Entidad = "Usuario", EntidadId = "1", FechaUtc = DateTime.UtcNow, Antes = "{\"estado\":\"Inactivo\",\"password\":\"clave-secreta\"}", Despues = "{\"estado\":\"Activo\",\"token\":\"token-secreto\"}" },
            new Auditoria { Usuario = foreign, SedeId = 6, Accion = "UPDATE", Entidad = "Habitacion", EntidadId = "2", FechaUtc = DateTime.UtcNow },
            new Auditoria { Usuario = client, SedeId = 5, Accion = "UPDATE", Entidad = "Reserva", EntidadId = "3", FechaUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = new AuditoriaController(db, new Actor(5)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var response = (await controller.Get(ct: default)).Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        json.Should().Contain("1@example.test").And.NotContain("2@example.test").And.NotContain("3@example.test").And.NotContain("secret");
        var detail = (await controller.Detalle(10, default)).Should().BeOfType<OkObjectResult>().Subject;
        var detailJson = System.Text.Json.JsonSerializer.Serialize(detail.Value);
        detailJson.Should().Contain("Inactivo").And.Contain("Activo").And.NotContain("clave-secreta").And.NotContain("token-secreto");
        var foreignId = await db.Auditoria.Where(a => a.SedeId == 6).Select(a => a.Id).SingleAsync();
        (await controller.Detalle(foreignId, default)).Should().BeOfType<NotFoundResult>();
        var forbidden = new AuditoriaController(db, new Actor(5, RolUsuario.Recepcion));
        await FluentActions.Invoking(() => forbidden.Get(ct: default)).Should().ThrowAsync<ForbiddenAccessException>();
        await FluentActions.Invoking(() => forbidden.Detalle(10, default)).Should().ThrowAsync<ForbiddenAccessException>();
        var missing = new AuditoriaController(db, new Actor(null));
        await FluentActions.Invoking(() => missing.Get(ct: default)).Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Middleware_registra_operacion_exitosa_sin_parametros_ni_cuerpo()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var services = new ServiceCollection().AddScoped<IApplicationDbContext>(_ => new ApplicationDbContext(options)).BuildServiceProvider();
        await using var requestScope = services.CreateAsyncScope();
        var http = new DefaultHttpContext { RequestServices = requestScope.ServiceProvider, User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthClaimTypes.TipoUsuario, AuthClaimTypes.Personal)], "test")) };
        http.Request.Method = "GET"; http.Request.QueryString = new QueryString("?password=secret");
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = "Habitaciones", ActionName = "Get" }), "test"));
        var middleware = new AuditoriaPersonalMiddleware(context => { context.Response.StatusCode = 200; return Task.CompletedTask; }, NullLogger<AuditoriaPersonalMiddleware>.Instance);
        await middleware.InvokeAsync(http, new Actor(5), services.GetRequiredService<IServiceScopeFactory>());
        await using var verify = new ApplicationDbContext(options);
        var entry = await verify.Auditoria.SingleAsync();
        entry.Accion.Should().Be("OTRA"); entry.Entidad.Should().Be("Habitaciones"); entry.SedeId.Should().Be(5);
        entry.Despues.Should().NotContain("secret").And.NotContain("password");
    }

    [Fact]
    public async Task Middleware_no_duplica_auditoria_de_dominio_ni_registra_fallos()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var services = new ServiceCollection().AddScoped<IApplicationDbContext>(_ => new ApplicationDbContext(options)).BuildServiceProvider();
        await using var requestScope = services.CreateAsyncScope();
        var http = new DefaultHttpContext { RequestServices = requestScope.ServiceProvider, User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthClaimTypes.TipoUsuario, AuthClaimTypes.Personal)], "test")) };
        http.Request.Method = "PUT";
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = "Recepcion", ActionName = "CheckIn" }), "test"));
        var middleware = new AuditoriaPersonalMiddleware(async context => {
            var db = context.RequestServices.GetRequiredService<IApplicationDbContext>();
            db.Auditoria.Add(new Auditoria { UsuarioId = 1, SedeId = 5, FechaUtc = DateTime.UtcNow, Accion = "UPDATE", Entidad = "ReservaHabitacion", EntidadId = "1" });
            await db.SaveChangesAsync(); context.Response.StatusCode = 204;
        }, NullLogger<AuditoriaPersonalMiddleware>.Instance);
        await middleware.InvokeAsync(http, new Actor(5), services.GetRequiredService<IServiceScopeFactory>());
        await using var verify = new ApplicationDbContext(options);
        (await verify.Auditoria.CountAsync()).Should().Be(1);
        var failed = new AuditoriaPersonalMiddleware(context => { context.Response.StatusCode = 403; return Task.CompletedTask; }, NullLogger<AuditoriaPersonalMiddleware>.Instance);
        await failed.InvokeAsync(http, new Actor(5), services.GetRequiredService<IServiceScopeFactory>());
        (await verify.Auditoria.CountAsync()).Should().Be(1);
    }
}
