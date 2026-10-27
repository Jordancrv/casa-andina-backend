using System.Net;
using System.Text.Json;
using CasaAndina.Application.Chatbot.DTOs;
using CasaAndina.Application.Chatbot.Queries;
using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class GeminiTravelClientTests
{
    private static readonly EstadiaRecomendadaDto[] Opciones = [new(1, "Sede Lima", "Lima", "Standard", 250, 500, 2, "Doble", "", new("no_disponible", "Sin datos", null, null, []))];
    private static readonly PlanificarViajeQuery Consulta = new("Quiero descansar", new(2026, 11, 1), new(2026, 11, 3), 500);

    [Fact]
    public async Task Envia_clave_en_header_y_valida_respuesta_estructurada()
    {
        var handler = new Handler(Envelope("""{"resumen":"Opciones para tu viaje","destinos":[{"sedeId":1,"motivo":"Cumple tu presupuesto"}]}"""));
        var result = await Client(handler).ExplicarAsync(Consulta with { Historial = [new("usuario", "Prefiero descansar en Lima")] }, Opciones, default);
        result.Should().NotBeNull();
        result!.Destinos[0].SedeId.Should().Be(1);
        handler.ApiKey.Should().Be("test-secret");
        handler.Url.Should().NotContain("test-secret");
        handler.Body.Should().Contain("responseJsonSchema").And.Contain("systemInstruction");
        using var body = JsonDocument.Parse(handler.Body);
        var text = body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
        using var payload = JsonDocument.Parse(text);
        payload.RootElement.GetProperty("consulta").GetProperty("historial")[0].GetProperty("texto").GetString()
            .Should().Be("Prefiero descansar en Lima");
    }

    [Theory]
    [InlineData("{\"resumen\":\"Hola\",\"destinos\":[{\"sedeId\":999,\"motivo\":\"Inventado\"}]}")]
    [InlineData("{\"resumen\":\"Hola\",\"destinos\":[{\"sedeId\":1,\"motivo\":\"A\"},{\"sedeId\":1,\"motivo\":\"B\"}]}")]
    [InlineData("{\"resumen\":\"Hola\",\"destinos\":[]}")]
    [InlineData("{\"resumen\":\"Hola\",\"destinos\":[null]}")]
    [InlineData("no es JSON")]
    public async Task Rechaza_destinos_inventados_duplicados_y_respuestas_invalidas(string json)
    {
        (await Client(new Handler(Envelope(json))).ExplicarAsync(Consulta, Opciones, default)).Should().BeNull();
    }

    [Theory]
    [InlineData(429)] [InlineData(503)]
    public async Task Cuota_y_falla_del_proveedor_permiten_fallback(int status)
    {
        (await Client(new Handler("{}", (HttpStatusCode)status)).ExplicarAsync(Consulta, Opciones, default)).Should().BeNull();
    }

    [Fact]
    public async Task Sin_clave_no_envia_peticion_y_respuesta_truncada_no_se_utiliza()
    {
        var handler = new Handler("{}");
        (await Client(handler, "").ExplicarAsync(Consulta, Opciones, default)).Should().BeNull();
        handler.Calls.Should().Be(0);
        (await Client(new Handler("{\"candidates\":[{\"finishReason\":\"MAX_TOKENS\"}]}"))
            .ExplicarAsync(Consulta, Opciones, default)).Should().BeNull();
    }

    [Fact]
    public async Task Timeout_permite_fallback_pero_cancelacion_del_usuario_se_propaga()
    {
        var client = Client(new Handler("{}", cancela: true));
        (await client.ExplicarAsync(Consulta, Opciones, default)).Should().BeNull();
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var act = () => client.ExplicarAsync(Consulta, Opciones, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static string Envelope(string text) => JsonSerializer.Serialize(new
    { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text } } } } } });
    private static GeminiTravelClient Client(Handler handler, string key = "test-secret") => new(new HttpClient(handler),
        Options.Create(new GeminiOptions { ApiKey = key }), NullLogger<GeminiTravelClient>.Instance);
    private sealed class Handler(string response, HttpStatusCode status = HttpStatusCode.OK, bool cancela = false) : HttpMessageHandler
    {
        public string? ApiKey { get; private set; }
        public string Url { get; private set; } = "";
        public string Body { get; private set; } = "";
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (cancela) throw new TaskCanceledException();
            ApiKey = request.Headers.GetValues("x-goog-api-key").Single();
            Url = request.RequestUri!.ToString();
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new(status) { Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
