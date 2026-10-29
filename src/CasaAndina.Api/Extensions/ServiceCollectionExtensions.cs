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
                        .Include(user => user.Roles).ThenInclude(userRole => userRole.Rol).ThenInclude(role => role.Permisos)
                        .Include(user => user.Persona).ThenInclude(persona => persona.Cliente)
                        .SingleOrDefaultAsync(user => user.Id == userId, context.HttpContext.RequestAborted);
                    if (account is null || !account.Activo)
                    {
                        context.Fail("La cuenta ya no está activa.");
                        return;
                    }

                    // Rebuild authorization from current DB state on every request; never trust stale permissions.
                    var validRoles = account.Roles.Where(x => x.Rol.Activo &&
                        (x.Rol.SedeId == null || x.Rol.SedeId == account.SedeId)).ToArray();
                    var currentRoles = validRoles.Select(x => RolCodigo.Mapear(x.Rol.Codigo)).Where(x => x.HasValue).Select(x=>x!.Value.ToString()).Distinct().OrderBy(x=>x, StringComparer.Ordinal);
                    var tokenRoles = principal.FindAll(ClaimTypes.Role).Select(x=>x.Value).Distinct().OrderBy(x=>x, StringComparer.Ordinal);
                    if (!currentRoles.SequenceEqual(tokenRoles)) { context.Fail("Los roles cambiaron; inicia sesión nuevamente."); return; }
                    var identity = (ClaimsIdentity)principal.Identity!;
                    foreach (var claim in identity.FindAll(ClaimTypes.Role).Concat(identity.FindAll("permiso")).ToArray())
                        identity.RemoveClaim(claim);
                    foreach (var role in validRoles.Select(x => RolCodigo.Mapear(x.Rol.Codigo)).Where(x => x.HasValue).Distinct().OrderBy(x => RolCodigo.Prioridad(x!.Value)))
                        identity.AddClaim(new Claim(ClaimTypes.Role, role!.Value.ToString()));
                    if (account.Persona.Cliente is null && (account.SedeId.HasValue || validRoles.Any(x=>x.Rol.Codigo == "SUPERADMINISTRADOR")))
                        foreach (var permission in validRoles.SelectMany(x => x.Rol.Permisos).Select(x => x.PermisoCodigo).Distinct())
                            identity.AddClaim(new Claim("permiso", permission));

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
            foreach (var permission in PermisosSistema.Todos)
                options.AddPolicy(permission, p => p.RequireAuthenticatedUser().RequireClaim("permiso", permission));
            options.AddPolicy("habitaciones.consultar", p => p.RequireAuthenticatedUser().RequireAssertion(c => c.User.HasClaim("permiso", "habitaciones.ver") || c.User.HasClaim("permiso", "calendario.ver")));
            options.AddPolicy("indicadores.consultar", p => p.RequireAuthenticatedUser().RequireAssertion(c => c.User.HasClaim("permiso", "dashboard.ver") || c.User.HasClaim("permiso", "reportes.ver")));
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
                      .AllowCredentials()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}
