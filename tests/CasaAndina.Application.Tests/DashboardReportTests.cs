using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Dashboard.DTOs;
using CasaAndina.Application.Dashboard.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class DashboardReportTests
{
    [Theory]
    [InlineData("7d")]
    [InlineData("30d")]
    [InlineData("mes")]
    public async Task Reporte_reutiliza_periodo_y_sede_del_dashboard(string period)
    {
        await using var db = Context();
        var today = PeruBusinessTime.Today;
        var site = new Sede { Id = 1, Nombre = "Sede asignada", Codigo = "A", Ciudad = "Cusco", Region = "Sierra", Categoria = "Standard" };
        var other = new Sede { Id = 2, Nombre = "Sede ajena", Codigo = "B", Ciudad = "Lima", Region = "Costa", Categoria = "Standard" };
        var client = new Cliente { Id = 1, Persona = new Persona { Natural = new PersonaNatural { Nombres = "Privado" } } };
        db.Sedes.AddRange(site, other); db.Clientes.Add(client);
        db.Reservas.AddRange(
            Reservation(1, site, client, today, 100),
            Reservation(2, site, client, today.AddDays(-10), 200),
            Reservation(3, other, client, today, 9999));
        await db.SaveChangesAsync();
        var renderer = new CapturePdf();
        await using var services = Provider(db, new User(1), renderer);
        var result = await services.GetRequiredService<ISender>().Send(new GetDashboardReportQuery(period));
        var expectedStart = period == "mes" ? new DateTime(today.Year, today.Month, 1) : today.AddDays(period == "7d" ? -6 : -29);
        var expectedTotal = expectedStart <= today.AddDays(-10) ? 300 : 100;
        renderer.Data!.Kpis.ImporteReservadoSoles.Should().Be(expectedTotal);
        renderer.Data.SedeNombre.Should().Be("Sede asignada");
        renderer.Data.Periodo!.Inicio.Should().Be(expectedStart);
        renderer.Data.Periodo.Fin.Should().Be(today);
        (renderer.Data.Periodo.FinAnterior - renderer.Data.Periodo.InicioAnterior).Should().Be(today - expectedStart);
        result.Nombre.Should().Be($"Reporte-CasaAndina-{today:yyyyMMdd}-{period}.pdf");
        result.Contenido.Should().Equal(Encoding.ASCII.GetBytes("%PDF-test"));
    }

    [Theory]
    [InlineData(null, "7d")]
    [InlineData(1, "inventado")]
    public async Task Reporte_rechaza_sede_ausente_y_periodo_invalido_antes_de_renderizar(int? site, string period)
    {
        await using var db = Context();
        var renderer = new CapturePdf();
        await using var services = Provider(db, new User(site), renderer);
        var action = () => services.GetRequiredService<ISender>().Send(new GetDashboardReportQuery(period));
        if (site is null) await action.Should().ThrowAsync<ForbiddenAccessException>();
        else await action.Should().ThrowAsync<ValidationException>();
        renderer.Data.Should().BeNull();
    }

    [Fact]
    public async Task Reporte_cancelado_no_renderiza_documentos()
    {
        await using var db = Context(); var renderer = new CapturePdf();
        await using var services = Provider(db, new User(1), renderer);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var action = () => services.GetRequiredService<ISender>().Send(new GetDashboardReportQuery(), cancelled.Token);
        await action.Should().ThrowAsync<OperationCanceledException>(); renderer.Data.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pdf_se_genera_con_actividad_y_sin_datos(bool empty)
    {
        var data = Example(empty);
        var bytes = new DashboardReportPdfService().Generar(data);
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        bytes.Length.Should().BeGreaterThan(10000);
        var directory = Environment.GetEnvironmentVariable("CASAANDINA_DASHBOARD_PDF_QA_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, empty ? "reporte-dashboard-sin-actividad.pdf" : "reporte-dashboard-ejemplo.pdf"), bytes);
        }
    }

    private static DashboardBiDto Example(bool empty)
    {
        var today = new DateTime(2026, 10, 26);
        return new DashboardBiDto("Casa Andina Premium Cusco · Datos de demostración",
            empty ? new(0, 0, 0, 0, 0, 0, 0) : new(124, 98, 98765.43m, 75000, 76.5m, 82, 66),
            Enumerable.Range(0, 6).Select(i => new ReservaSemanaBiDto(i == 5 ? "Actual" : $"S-{5-i}", empty ? 0 : 12 + i * 3, empty ? 0 : i % 3, 0, 0)).ToArray(),
            [new("Casa Andina Premium Cusco", "Cusco", empty ? 0 : 76.5m, empty ? 0 : 26)], empty ? 0 : 8,
            empty ? new(0, 0, 0, 0, 0) : new(80, 32, 116, 851.43m, 2.4m),
            new("30d", today.AddDays(-29), today, today.AddDays(-59), today.AddDays(-30), today.AddDays(-35), new DateTime(2026, 10, 26, 18, 30, 0, DateTimeKind.Utc)));
    }
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Reserva Reservation(int id, Sede site, Cliente client, DateTime date, decimal amount) => new()
    {
        Id = id, CodigoReserva = $"QA-{id}", Sede = site, SedeId = site.Id, Cliente = client, ClienteId = client.Id,
        Estado = "Reservado", Canal = "Directo", PrecioTotal = amount, FechaCreacion = PeruBusinessTime.UtcStartOfDay(date).AddHours(1)
    };
    private static ServiceProvider Provider(ApplicationDbContext db, User user, CapturePdf renderer)
    {
        var services = new ServiceCollection();
        services.AddApplication(); services.AddLogging();
        services.AddSingleton<IApplicationDbContext>(db); services.AddSingleton<ICurrentUserService>(user);
        services.AddSingleton<IDashboardReportPdfService>(renderer);
        return services.BuildServiceProvider();
    }
    private sealed class CapturePdf : IDashboardReportPdfService
    {
        public DashboardBiDto? Data { get; private set; }
        public byte[] Generar(DashboardBiDto dashboard) { Data = dashboard; return Encoding.ASCII.GetBytes("%PDF-test"); }
    }
    private sealed class User(int? site) : ICurrentUserService
    {
        public int? UsuarioId => 1; public int? ClienteId => null; public int? SedeId => site; public RolUsuario? Rol => RolUsuario.Administrador;
    }
}
