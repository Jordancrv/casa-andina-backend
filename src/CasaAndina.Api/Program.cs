using CasaAndina.Api.Extensions;
using CasaAndina.Api.Middleware;
using CasaAndina.Application;
using CasaAndina.Infrastructure;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    var recaptcha = builder.Configuration.GetSection("GoogleRecaptcha");
    if (!recaptcha.GetValue<bool>("Enabled")
        || string.IsNullOrWhiteSpace(recaptcha["SecretKey"])
        || !(recaptcha.GetSection("AllowedHostnames").Get<string[]>()?.Any(hostname => !string.IsNullOrWhiteSpace(hostname)) == true))
    {
        throw new InvalidOperationException(
            "Google reCAPTCHA debe configurarse fuera de Development: habilítelo y configure la clave secreta y los hostnames permitidos.");
    }
}

// Cada capa registra sus propios servicios (Clean Architecture):
// Application no conoce EF/JWT; Infrastructure los implementa.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCorsForFrontend(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfter = TimeSpan.FromMinutes(1);
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var leaseRetryAfter))
            retryAfter = leaseRetryAfter;

        var retrySeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        context.HttpContext.Response.Headers.RetryAfter = retrySeconds.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        var retryMinutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Demasiadas solicitudes",
            Detail = $"Has alcanzado el límite de solicitudes. Intenta nuevamente en aproximadamente {retryMinutes} minuto(s)."
        };
        await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    };
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("register", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("asistente", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 6,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("calendario", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Evita colisiones entre DTOs con el mismo nombre en módulos distintos.
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Ingresar: Bearer {token}"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Solo en producción: en desarrollo el frontend usa HTTP y
    // UseHttpsRedirection devuelve 307 que rompe CORS en preflight OPTIONS.
    app.UseHttpsRedirection();
}

app.UseCors("FrontendPolicy");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
