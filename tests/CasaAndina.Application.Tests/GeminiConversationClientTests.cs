using System.Net;
using System.Text.Json;
using CasaAndina.Application.Chatbot.Queries;
using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class GeminiConversationClientTests
{
    [Fact]
    public async Task Interpreta_mensaje_sin_fechas_y_envia_clave_en_header()
    {
        var handler = new Handler(Envelope("""{"mensaje":"¡Hola! ¿A dónde te gustaría viajar?","preferencias":{"fechaEntrada":null,"fechaSalida":null,"presupuestoPorNoche":null,"adultos":null,"ciudad":null,"destinoFlexible":false},"buscarOpciones":false}"""));
        var result = await Client(handler).InterpretarAsync(new("Hola"), ["Cusco"], new(2026,10,27), default);
        result.Should().NotBeNull(); result!.Preferencias.FechaEntrada.Should().BeNull(); result.BuscarOpciones.Should().BeFalse();
        handler.Key.Should().Be("test-only"); handler.Url.Should().NotContain("test-only");
        handler.Body.Should().Contain("ciudadesDisponibles").And.Contain("2026-10-27");
    }
    [Fact]
    public async Task Acepta_fechas_civiles_y_rechaza_ciudad_inventada()
    {
        const string valid = """{"mensaje":"Revisemos las opciones","preferencias":{"fechaEntrada":"2026-10-28","fechaSalida":"2026-10-30","presupuestoPorNoche":400,"adultos":2,"ciudad":"Cusco","destinoFlexible":false},"buscarOpciones":true}""";
        var result = await Client(new(Envelope(valid))).InterpretarAsync(new("Voy a Cusco"), ["Cusco"], new(2026,10,27), default);
        result.Should().NotBeNull(); result!.Preferencias.FechaEntrada.Should().Be(new DateTime(2026,10,28));
        var unknown = await Client(new(Envelope(valid.Replace("Cusco", "Inventado")))).InterpretarAsync(new("Hola"), ["Cusco"], new(2026,10,27), default);
        unknown.Should().BeNull();
    }
    [Theory]
    [InlineData(429)] [InlineData(503)]
    public async Task Cuota_y_proveedor_caido_habilitan_respaldo(int status)
        => (await Client(new("{}",(HttpStatusCode)status)).InterpretarAsync(new("Hola"), ["Cusco"], new(2026,10,27), default)).Should().BeNull();
    [Fact]
    public async Task Sin_clave_no_consume_proveedor()
    {
        var handler = new Handler("{}");
        (await Client(handler, "").InterpretarAsync(new("Hola"), ["Cusco"], new(2026,10,27), default)).Should().BeNull();
        handler.Calls.Should().Be(0);
    }
    private static string Envelope(string text) => JsonSerializer.Serialize(new { candidates = new[] { new { finishReason="STOP", content=new {parts=new[] {new {text}}} } } });
    private static GeminiConversationClient Client(Handler handler,string key="test-only") => new(new HttpClient(handler), Options.Create(new GeminiOptions {ApiKey=key}), NullLogger<GeminiConversationClient>.Instance);
    private sealed class Handler(string response,HttpStatusCode status=HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls {get;private set;} public string? Key {get;private set;} public string Url {get;private set;}=""; public string Body {get;private set;}="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++; Key=request.Headers.GetValues("x-goog-api-key").Single(); Url=request.RequestUri!.ToString(); Body=await request.Content!.ReadAsStringAsync(ct);
            return new(status) {Content=new StringContent(response,System.Text.Encoding.UTF8,"application/json")};
        }
    }
}
