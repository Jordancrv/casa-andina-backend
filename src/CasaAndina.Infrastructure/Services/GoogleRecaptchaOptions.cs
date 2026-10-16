namespace CasaAndina.Infrastructure.Services;

public sealed class GoogleRecaptchaOptions
{
    public bool Enabled { get; set; }
    public string? SecretKey { get; set; }
    public string[] AllowedHostnames { get; set; } = [];
}
