using System.Text;
using System.Security.Claims;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CasaAndina.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var jwtKey = jwtSettings["Key"];

        if (string.IsNullOrWhiteSpace(jwtKey)
            || jwtKey.Length < 32
            || jwtKey.StartsWith("CAMBIAR-", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Jwt:Key debe configurarse fuera del repositorio y tener al menos 32 caracteres.");
        }

        _ = CasaAndina.Infrastructure.Services.JwtTokenSettings.ValidateAndGetExpirationHours(configuration);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey))
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.Principal?.FindFirstValue("sub");
                    if (!int.TryParse(userIdValue, out var userId))
                    {
                        context.Fail("El token no identifica una cuenta válida.");
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
                    var isActive = await db.Usuarios.AsNoTracking()
                        .AnyAsync(user => user.Id == userId && user.Activo, context.HttpContext.RequestAborted);
                    if (!isActive)
                        context.Fail("La cuenta ya no está activa.");
                }
            };
        });

        // RBAC (RNF01): políticas por rol, usadas con [Authorize(Policy = "Administrador")]
        services.AddAuthorization(options =>
        {
            options.AddPolicy(nameof(RolUsuario.Superadministrador), p => p.RequireRole(nameof(RolUsuario.Superadministrador)));
            options.AddPolicy(nameof(RolUsuario.Administrador), p => p.RequireRole(nameof(RolUsuario.Administrador)));
            options.AddPolicy(nameof(RolUsuario.Recepcion), p => p.RequireRole(nameof(RolUsuario.Recepcion)));
            options.AddPolicy(nameof(RolUsuario.Operaciones), p => p.RequireRole(nameof(RolUsuario.Operaciones)));
            options.AddPolicy(nameof(RolUsuario.Mantenimiento), p => p.RequireRole(nameof(RolUsuario.Mantenimiento)));
            options.AddPolicy(nameof(RolUsuario.Cliente), p => p.RequireRole(nameof(RolUsuario.Cliente)));
            options.AddPolicy("PersonalInterno", p => p.RequireRole(
                nameof(RolUsuario.Superadministrador),
                nameof(RolUsuario.Administrador),
                nameof(RolUsuario.Recepcion),
                nameof(RolUsuario.Operaciones),
                nameof(RolUsuario.Mantenimiento)));
            options.AddPolicy("GestionHabitaciones", p => p.RequireRole(
                nameof(RolUsuario.Superadministrador),
                nameof(RolUsuario.Administrador),
                nameof(RolUsuario.Operaciones),
                nameof(RolUsuario.Mantenimiento)));
            options.AddPolicy("GestionServicios", p => p.RequireRole(
                nameof(RolUsuario.Superadministrador),
                nameof(RolUsuario.Administrador),
                nameof(RolUsuario.Operaciones)));
        });

        return services;
    }

    public static IServiceCollection AddCorsForFrontend(
        this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddPolicy("FrontendPolicy", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}
