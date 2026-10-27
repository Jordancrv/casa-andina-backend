using System.Net;
using System.Text.Json;
using CasaAndina.Application.Common;
using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class OpenMeteoWeatherTests
{
    [Fact]
    public async Task Fuera_del_horizonte_no_llama_api_ni_inventa_clima()
    {
        var handler = new Handler();
        var result = await Service(handler).ConsultarAsync("Lima", PeruBusinessTime.Today.AddDays(16), PeruBusinessTime.Today.AddDays(18), default);
        result.Estado.Should().Be("fuera_de_rango");
        result.Dias.Should().BeEmpty(); handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Consulta_fechas_correctas_incluye_salida_y_reutiliza_cache()
    {
        var entrada = PeruBusinessTime.Today.AddDays(1);
        var handler = new Handler(entrada, 3);
        var service = Service(handler);
        var result = await service.ConsultarAsync("Lima", entrada, entrada.AddDays(2), default);
        result.Estado.Should().Be("disponible"); result.Dias.Should().HaveCount(3);
        result.FuenteUrl.Should().Be("https://open-meteo.com/");
        handler.ForecastUrl.Should().Contain($"start_date={entrada:yyyy-MM-dd}").And.Contain($"end_date={entrada.AddDays(2):yyyy-MM-dd}");
        (await service.ConsultarAsync("Lima", entrada, entrada.AddDays(2), default)).Should().BeSameAs(result);
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Estadias_parcialmente_fuera_del_horizonte_no_extrapolan()
    {
        var entrada = PeruBusinessTime.Today.AddDays(14);
        var handler = new Handler(entrada, 2);
        var result = await Service(handler).ConsultarAsync("Lima", entrada, entrada.AddDays(4), default);
        result.Estado.Should().Be("parcial"); result.Dias.Should().HaveCount(2);
        handler.ForecastUrl.Should().Contain($"end_date={PeruBusinessTime.Today.AddDays(15):yyyy-MM-dd}");
    }

    [Theory]
    [InlineData("US", "Lima")]
    [InlineData("PE", "Otra ciudad")]
    public async Task No_usa_una_ciudad_homonima_o_de_otro_pais(string pais, string ciudad)
    {
        var handler = new Handler(pais: pais, ciudad: ciudad);
        var result = await Service(handler).ConsultarAsync("Lima", PeruBusinessTime.Today, PeruBusinessTime.Today.AddDays(2), default);
        result.Estado.Should().Be("sin_ubicacion"); handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Pronostico_incompleto_no_muestra_temperaturas_nulas_como_cero()
    {
        var handler = new Handler(PeruBusinessTime.Today, 3, nullTemperature: true);
        var result = await Service(handler).ConsultarAsync("Lima", PeruBusinessTime.Today, PeruBusinessTime.Today.AddDays(2), default);
        result.Estado.Should().Be("parcial"); result.Dias.Should().HaveCount(2);
        result.Dias.Should().OnlyContain(d => d.MinimaC == 15);
    }

    [Fact]
    public async Task Fallo_externo_se_muestra_como_no_disponible()
    {
        var handler = new Handler(status: HttpStatusCode.ServiceUnavailable);
        var result = await Service(handler).ConsultarAsync("Lima", PeruBusinessTime.Today, PeruBusinessTime.Today.AddDays(2), default);
        result.Estado.Should().Be("no_disponible"); result.Dias.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)] [InlineData(61)] [InlineData(95)] [InlineData(null)]
    public async Task Conserva_condicion_WMO_y_no_infiere_sol_cuando_falta(int? codigo)
    {
        var handler = new Handler(PeruBusinessTime.Today, codigo: codigo);
        var result = await Service(handler).ConsultarAsync("Lima", PeruBusinessTime.Today, PeruBusinessTime.Today, default);
        result.Dias.Should().ContainSingle().Which.CodigoTiempo.Should().Be(codigo);
        handler.ForecastUrl.Should().Contain("weather_code");
    }

    private static OpenMeteoWeatherService Service(Handler handler) => new(new HttpClient(handler),
        new MemoryCache(new MemoryCacheOptions()), NullLogger<OpenMeteoWeatherService>.Instance);

    private sealed class Handler(DateTime inicio = default, int dias = 1, string pais = "PE", string ciudad = "Lima",
        bool nullTemperature = false, HttpStatusCode status = HttpStatusCode.OK, int? codigo = null) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string ForecastUrl { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            object body;
            if (request.RequestUri!.Host == "geocoding-api.open-meteo.com")
                body = new { results = new[] { new { name = ciudad, country_code = pais, latitude = -12.05, longitude = -77.05, population = 1000000 } } };
            else
            {
                ForecastUrl = request.RequestUri.ToString();
                body = new { daily = new
                {
                    time = Enumerable.Range(0, dias).Select(i => inicio.AddDays(i).ToString("yyyy-MM-dd")).ToArray(),
                    temperature_2m_min = Enumerable.Range(0, dias).Select(i => nullTemperature && i == 0 ? (double?)null : 15d).ToArray(),
                    temperature_2m_max = Enumerable.Repeat(22d, dias).ToArray(),
                    precipitation_probability_max = Enumerable.Repeat(20d, dias).ToArray(),
                    weather_code = Enumerable.Repeat(codigo, dias).ToArray()
                } };
            }
            return Task.FromResult(new HttpResponseMessage(status)
            { Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json") });
        }
    }
}
