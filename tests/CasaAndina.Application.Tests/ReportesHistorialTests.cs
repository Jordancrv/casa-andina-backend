using System.Text.Json;
using CasaAndina.Api.Controllers;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Dashboard.DTOs;
using CasaAndina.Application.Reportes;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ReportesHistorialTests
{
    private sealed record User(int? SedeId) : ICurrentUserService
    {
        public RolUsuario? Rol => RolUsuario.Administrador;
        public int? UsuarioId => 1;
        public int? ClienteId => null;
    }
    private sealed class Analysis : IAnalisisReporteService
    {
        public AnalisisReporte? Consultar(int sedeId, string periodo) => null;
        public Task<AnalisisReporte?> ObtenerAsync(int sedeId, string periodo, DashboardBiDto datos, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
    private sealed class Pdf : IAnalisisReportePdfService { public byte[] Generar(AnalisisReporte a) => [1, 2]; }
    [Fact]
    public async Task Historial_detalle_y_pdf_no_exponen_otras_sedes()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTime.UtcNow;
        var dto = new DashboardBiDto("Sede 2", new(1,0,10,0,0,1,0), [], [], 0, new(1,0,1,10,1));
        var json = JsonSerializer.Serialize(new AnalisisReporte("R", "H", "A", "L", now, dto), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        db.ReportesAnalisis.Add(new ReporteAnalisis { Id = 42, SedeId = 2, UsuarioId = 1, Periodo = "30d", GeneradoUtc = now, ContenidoJson = json });
        await db.SaveChangesAsync();
        var controller = new ReportesController(null!, new User(1), new Analysis(), new Pdf(), db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        (await controller.Detalle(42, default)).Should().BeOfType<NotFoundResult>();
        (await controller.Descargar(42, default)).Should().BeOfType<NotFoundResult>();
        var own = new ReportesController(null!, new User(2), new Analysis(), new Pdf(), db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        (await own.Detalle(42, default)).Should().BeOfType<OkObjectResult>();
        (await own.Descargar(42, default)).Should().BeOfType<FileContentResult>();
    }
}
