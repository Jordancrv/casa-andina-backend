using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Identity;
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
                    var principal = context.Principal;
                    if (principal is null)
                    {
                        context.Fail("El token no contiene una identidad válida.");
                        return;
                    }

                    var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? principal.FindFirstValue("sub");
                    if (!int.TryParse(userIdValue, out var userId))
                    {
                        context.Fail("El token no identifica una cuenta válida.");
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
                    var account = await db.Usuarios.AsNoTracking()
                        .Include(user => user.Roles).ThenInclude(userRole => userRole.Rol)
                        .Include(user => user.Persona).ThenInclude(persona => persona.Cliente)
                        .SingleOrDefaultAsync(user => user.Id == userId, context.HttpContext.RequestAborted);
                    if (account is null || !account.Activo)
                    {
                        context.Fail("La cuenta ya no está activa.");
                        return;
                    }

                    var currentRoles = account.Roles
                        .Select(userRole => RolCodigo.Mapear(userRole.Rol.Codigo))
                        .Where(role => role.HasValue)
                        .Select(role => role!.Value.ToString())
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(role => role, StringComparer.Ordinal);
                    var tokenRoles = principal.FindAll(ClaimTypes.Role)
                        .Select(claim => claim.Value)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(role => role, StringComparer.Ordinal);
                    if (!currentRoles.SequenceEqual(tokenRoles, StringComparer.Ordinal))
                    {
                        context.Fail("Los roles de la cuenta cambiaron; inicia sesión nuevamente.");
                        return;
                    }

                    var tokenSedeId = principal.FindFirstValue(AuthClaimTypes.SedeId);
                    var currentSedeId = account.SedeId?.ToString();
                    if (!string.Equals(tokenSedeId, currentSedeId, StringComparison.Ordinal))
                    {
                        context.Fail("La sede asignada cambió; inicia sesión nuevamente.");
                        return;
                    }

                    var tokenClienteId = principal.FindFirstValue(AuthClaimTypes.ClienteId);
                    var currentClienteId = account.Persona.Cliente?.Id.ToString();
                    if (!string.Equals(tokenClienteId, currentClienteId, StringComparison.Ordinal))
                    {
                        context.Fail("El perfil de cliente cambió; inicia sesión nuevamente.");
                        return;
                    }

                    var currentTipoUsuario = account.Persona.Cliente is null
                        ? AuthClaimTypes.Personal
                        : AuthClaimTypes.Cliente;
                    if (!string.Equals(
                            principal.FindFirstValue(AuthClaimTypes.TipoUsuario),
                            currentTipoUsuario,
                            StringComparison.Ordinal))
                    {
                        context.Fail("El tipo de cuenta cambió; inicia sesión nuevamente.");
                        return;
                    }

                    var tokenEmail = principal.FindFirstValue(ClaimTypes.Email)
                        ?? principal.FindFirstValue(JwtRegisteredClaimNames.Email);
                    if (!string.Equals(tokenEmail?.Trim(), account.Correo.Trim(), StringComparison.OrdinalIgnoreCase))
                        context.Fail("El correo de acceso cambió; inicia sesión nuevamente.");
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
