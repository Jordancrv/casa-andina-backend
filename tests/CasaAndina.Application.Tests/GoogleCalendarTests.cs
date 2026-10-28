using System.Net;
using System.Text;
using System.Text.Json;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Domain.Entities;
using CasaAndina.Infrastructure.Persistence;
using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class GoogleCalendarTests
{
    [Fact]
    public async Task OAuth_requires_the_same_browser_and_consumes_state_once_encrypting_the_token()
    {
        using var fixture = new Fixture();
        var url = fixture.Service.CrearAutorizacion(1, 10, "browser");
        var query = QueryHelpers.ParseQuery(new Uri(url).Query);
        query["scope"].ToString().Should().Be(GoogleCalendarClient.Scope);
        query["code_challenge_method"].ToString().Should().Be("S256");
        query["code_challenge"].ToString().Length.Should().BeGreaterThan(30);
        var state = query["state"].ToString();
        var wrongBrowser = () => fixture.Service.CompletarAutorizacionAsync(state, "code", "another-browser", default);
        await wrongBrowser.Should().ThrowAsync<ConflictException>();
        fixture.Http.Requests.Should().BeEmpty();
        await fixture.Service.CompletarAutorizacionAsync(state, "code", "browser", default);
        var connection = await fixture.Db.Set<ConexionCalendario>().SingleAsync();
        connection.RefreshTokenProtegido.Should().NotContain("test-refresh-token");
        fixture.Protection.CreateProtector("CasaAndina.GoogleCalendar.RefreshToken.v1")
            .Unprotect(connection.RefreshTokenProtegido!).Should().Be("test-refresh-token");
        var replay = () => fixture.Service.CompletarAutorizacionAsync(state, "code", "browser", default);
        await replay.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task OAuth_rejects_unknown_state_or_a_deactivated_account()
    {
        using var f = new Fixture();
        var unknown = () => f.Service.CompletarAutorizacionAsync("missing", "code", "browser", default);
        await unknown.Should().ThrowAsync<ConflictException>();
        var url = f.Service.CrearAutorizacion(1, 10, "browser");
        f.Db.Usuarios.Single().Activo = false;
        await f.Db.SaveChangesAsync();
        var state = QueryHelpers.ParseQuery(new Uri(url).Query)["state"].ToString();
        var inactive = () => f.Service.CompletarAutorizacionAsync(state, "code", "browser", default);
        await inactive.Should().ThrowAsync<ConflictException>();
        f.Http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Sync_is_scoped_idempotent_and_updates_dates_then_removes_cancelled_events()
    {
        using var f = new Fixture();
        await f.Connect();
        f.AddReservation(100, 10, "Confirmada");
        f.AddReservation(200, 20, "Confirmada");
        f.AddReservation(300, 10, "Pendiente");
        await f.Db.SaveChangesAsync();
        var first = await f.Service.SincronizarAsync(10, default);
        first.Actualizados.Should().Be(1);
        f.Http.Events.Should().ContainSingle();
        var eventJson = f.Http.Events.Values.Single();
        eventJson.Should().Contain("CA-100").And.NotContain("CA-200").And.NotContain("CA-300");
        var savedId = f.Db.Set<EventoCalendario>().Single().EventoId;
        var second = await f.Service.SincronizarAsync(10, default);
        second.Actualizados.Should().Be(0);
        f.Http.Events.Should().ContainSingle();
        var reservation = f.Db.Reservas.Single(r => r.Id == 100);
        reservation.Habitaciones.Single().FechaCheckOut = new DateTime(2026, 12, 7);
        await f.Db.SaveChangesAsync();
        (await f.Service.SincronizarAsync(10, default)).Actualizados.Should().Be(1);
        f.Db.Set<EventoCalendario>().Single().EventoId.Should().Be(savedId);
        f.Http.Events.Values.Single().Should().Contain("2026-12-07");
        reservation.Estado = "Cancelada";
        await f.Db.SaveChangesAsync();
        (await f.Service.SincronizarAsync(10, default)).Eliminados.Should().Be(1);
        f.Http.Events.Should().BeEmpty();
        f.Db.Set<EventoCalendario>().Should().BeEmpty();
    }

    [Fact]
    public async Task A_timeout_after_google_creates_the_event_does_not_duplicate_it_on_retry()
    {
        using var f = new Fixture();
        await f.Connect(); f.AddReservation(100, 10, "Confirmada"); await f.Db.SaveChangesAsync();
        f.Http.FailAfterInsert = true;
        var first = () => f.Service.SincronizarAsync(10, default);
        await first.Should().ThrowAsync<HttpRequestException>();
        f.Http.Events.Should().ContainSingle();
        await f.Service.SincronizarAsync(10, default);
        f.Http.Events.Should().ContainSingle();
        f.Db.Set<EventoCalendario>().Single().Huella.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Expired_permission_requests_reconnection_without_disclosing_tokens()
    {
        using var f = new Fixture(); await f.Connect(); f.Http.InvalidGrant = true;
        var sync = () => f.Service.SincronizarAsync(10, default);
        await sync.Should().ThrowAsync<CalendarRemoteException>().WithMessage("Vuelve a conectar*");
        (await f.Service.EstadoAsync(10, default)).RequiereReconectar.Should().BeTrue();
        (await f.Service.EstadoAsync(20, default)).Conectado.Should().BeFalse();
    }

    [Fact]
    public async Task Disconnect_revokes_access_and_preserves_existing_calendar_events()
    {
        using var f = new Fixture(); await f.Connect(); f.AddReservation(100, 10, "Confirmada"); await f.Db.SaveChangesAsync();
        await f.Service.SincronizarAsync(10, default);
        await f.Service.DesconectarAsync(10, default);
        f.Http.Requests.Should().Contain(x => x.Contains("/revoke"));
        (await f.Service.EstadoAsync(10, default)).Conectado.Should().BeFalse();
        f.Http.Events.Should().ContainSingle();
        var sync = () => f.Service.SincronizarAsync(10, default);
        await sync.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public void All_day_event_uses_checkout_as_exclusive_end_and_contains_no_payment_or_guest_details()
    {
        var reservation = new Reserva { CodigoReserva = "CA-123", Sede = new() { Nombre = "Casa Andina", Ciudad = "Cusco", Direccion = "Calle 1" },
            Habitaciones = [new() { FechaCheckIn = new(2026, 12, 1), FechaCheckOut = new(2026, 12, 3) },
                new() { FechaCheckIn = new(2026, 12, 2), FechaCheckOut = new(2026, 12, 4) }] };
        var result = GoogleCalendarService.CrearEvento(reservation);
        result.start.date.Should().Be("2026-12-01"); result.end.date.Should().Be("2026-12-04");
        result.visibility.Should().Be("private"); result.reminders.overrides.Single().minutes.Should().Be(1440);
        result.location.Should().Be("Calle 1, Cusco");
    }

    private sealed class Fixture : IDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public IDataProtectionProvider Protection { get; } = new EphemeralDataProtectionProvider();
        public FakeGoogle Http { get; } = new();
        private readonly MemoryCache cache = new(new MemoryCacheOptions());
        public GoogleCalendarService Service { get; }
        public Fixture()
        {
            var options = Options.Create(new GoogleCalendarOptions { ClientId = "test-id", ClientSecret = "test-secret" });
            Service = new(Db, new GoogleCalendarClient(new HttpClient(Http), options), options, Protection, cache, new());
            var person = new Persona { Id = 1, Cliente = new() { Id = 10 } };
            Db.Usuarios.Add(new() { Id = 1, Persona = person, Correo = "test@example.test", PasswordHash = "hash", Activo = true,
                Roles = [new() { Rol = new() { Id = 1, Codigo = "CLIENTE", Nombre = "Cliente" } }] });
            Db.SaveChanges();
        }
        public async Task Connect()
        {
            var url = Service.CrearAutorizacion(1, 10, "browser");
            await Service.CompletarAutorizacionAsync(QueryHelpers.ParseQuery(new Uri(url).Query)["state"].ToString(), "code", "browser", default);
        }
        public void AddReservation(int id, int client, string state) => Db.Reservas.Add(new() { Id = id, ClienteId = client,
            CodigoReserva = $"CA-{id}", Estado = state, Canal = "Directo", Sede = new() { Id = id, Nombre = "Casa Andina", Ciudad = "Cusco", Codigo = $"T{id}", Region = "Sierra", Categoria = "Standard" },
            Habitaciones = [new() { Id = id, HabitacionId = id, FechaCheckIn = new(2026, 12, 1), FechaCheckOut = new(2026, 12, 3) }] });
        public void Dispose() { Db.Dispose(); cache.Dispose(); Http.Dispose(); }
    }

    private sealed class FakeGoogle : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public Dictionary<string, string> Events { get; } = [];
        public bool InvalidGrant { get; set; }
        public bool FailAfterInsert { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(request.Method + " " + path);
            if (path == "/token") return InvalidGrant ? Json("{\"error\":\"invalid_grant\"}", HttpStatusCode.BadRequest)
                : Json(JsonSerializer.Serialize(new { access_token = "access", refresh_token = "test-refresh-token", scope = GoogleCalendarClient.Scope }));
            if (path == "/revoke") return Json("{}");
            if (!path.Contains("/events")) return Json("{\"id\":\"calendar@test\"}");
            var id = path.Split('/').Last();
            if (request.Method == HttpMethod.Put)
            {
                if (!Events.ContainsKey(id)) return Json("{}", HttpStatusCode.NotFound);
                Events[id] = await request.Content!.ReadAsStringAsync(ct); return Json("{}");
            }
            if (request.Method == HttpMethod.Delete) { Events.Remove(id); return Json("{}"); }
            var content = await request.Content!.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content); id = doc.RootElement.GetProperty("id").GetString()!;
            Events[id] = content;
            if (FailAfterInsert) { FailAfterInsert = false; throw new HttpRequestException("Simulated timeout after commit"); }
            return Json("{}");
        }
        private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
