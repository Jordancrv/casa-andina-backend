using CasaAndina.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class GoogleRecaptchaVerifierTests
{
    [Fact]
    public async Task Verifier_acepta_token_exitoso_de_hostname_permitido()
    {
        using var client = new HttpClient(new ResponseHandler("{\"success\":true,\"hostname\":\"localhost\"}"));
        var verifier = CreateVerifier(client, ["localhost"]);

        var result = await verifier.VerifyAsync("token-de-prueba", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Verifier_rechaza_hostname_ajeno_y_token_vacio()
    {
        using var client = new HttpClient(new ResponseHandler("{\"success\":true,\"hostname\":\"otro.example\"}"));
        var verifier = CreateVerifier(client, ["localhost"]);

        (await verifier.VerifyAsync("token-de-prueba", CancellationToken.None)).Should().BeFalse();
        (await verifier.VerifyAsync(" ", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task Verifier_mantiene_desarrollo_local_si_no_esta_configurado()
    {
        using var client = new HttpClient(new ResponseHandler("{}"));
        var verifier = CreateVerifier(client, [], enabled: false);

        var result = await verifier.VerifyAsync(null, CancellationToken.None);

        result.Should().BeTrue();
    }

    private static GoogleRecaptchaVerifier CreateVerifier(HttpClient client, string[] allowedHostnames, bool enabled = true)
        => new(
            client,
            Options.Create(new GoogleRecaptchaOptions
            {
                Enabled = enabled,
                SecretKey = "test-only-secret",
                AllowedHostnames = allowedHostnames
            }),
            new TestEnvironment(),
            NullLogger<GoogleRecaptchaVerifier>.Instance);

    private sealed class ResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "CasaAndina.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
