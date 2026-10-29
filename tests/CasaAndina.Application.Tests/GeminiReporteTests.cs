using System.Net;
using System.Text;
using CasaAndina.Application.Dashboard.DTOs;
using CasaAndina.Application.Reportes;
using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class GeminiReporteTests
{
    [Fact]
    public void Pdf_incluye_analisis_y_graficas_con_textos_largos()
    {
        var now = DateTime.UtcNow;
        var dto = new DashboardBiDto("Sede prueba", new(3, 2, 100, 80, 50, 2, 1),
            [new("Actual", 3, 1, 100, 33)], [], 1, new(2, 0, 2, 50, 1),
            new("30d", now.Date.AddDays(-29), now.Date, now.Date.AddDays(-59), now.Date.AddDays(-30), now.Date.AddDays(-35), now));
        var report = new AnalisisReporte("Resumen", new string('A', 1500), "Acciones sugeridas", "Limitaciones", now, dto);
        var bytes = new AnalisisReportePdfService().Generar(report);
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        public string Body = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"parts\":[{\"text\":\"{\\\"resumen\\\":\\\"Resumen\\\",\\\"hallazgos\\\":\\\"Hallazgos\\\",\\\"recomendaciones\\\":\\\"Acciones\\\",\\\"limitaciones\\\":\\\"Limites\\\"}\"}]}}]}", Encoding.UTF8, "application/json") };
        }
    }
    private sealed class Factory(Handler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
    [Fact]
    public async Task Cache_separa_sede_y_periodo_y_no_envia_nombre_de_sede()
    {
        var handler = new Handler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new GeminiReporteService(new Factory(handler), Options.Create(new GeminiOptions { ApiKey = "test", Model = "test-model" }), cache);
        var dto = new DashboardBiDto("Nombre privado", new(2, 1, 100, 80, 50, 2, 1), [], [], 0, new(2, 0, 2, 50, 1));
        (await service.ObtenerAsync(1, "7d", dto, default)).Should().NotBeNull();
        (await service.ObtenerAsync(1, "7d", dto, default)).Should().NotBeNull();
        handler.Calls.Should().Be(1);
        handler.Body.Should().NotContain("Nombre privado");
        await service.ObtenerAsync(2, "7d", dto, default);
        await service.ObtenerAsync(1, "30d", dto, default);
        handler.Calls.Should().Be(3);
    }
}
