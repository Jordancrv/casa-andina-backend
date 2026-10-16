using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CasaAndina.Infrastructure.Services;

/// <summary>RNF01: emisión de JWT con el rol embebido como claim para el RBAC.</summary>
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration) => _configuration = configuration;

    public string GenerarToken(Usuario usuario)
    {
        var persona = usuario.Persona;
        var email = persona.Contactos
            .FirstOrDefault(x => x.TipoContacto == "Correo" && x.EsPrincipal)?.Valor
            ?? persona.Contactos.FirstOrDefault(x => x.TipoContacto == "Correo")?.Valor
            ?? throw new InvalidOperationException("La persona del usuario no tiene correo electrónico.");
        var nombre = persona.Natural is not null
            ? string.Join(' ', new[] { persona.Natural.Nombres, persona.Natural.Apellidos }
                .Where(x => !string.IsNullOrWhiteSpace(x)))
            : persona.Juridica?.RazonSocial ?? "Usuario";
        var roles = usuario.Roles
            .Select(x => RolCodigo.Mapear(x.Rol.Codigo))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .OrderBy(RolCodigo.Prioridad)
            .ToArray();
        if (roles.Length == 0)
            throw new InvalidOperationException("El usuario no tiene roles asignados.");

        var jwtSettings = _configuration.GetSection("Jwt");
        var expirationHours = JwtTokenSettings.ValidateAndGetExpirationHours(_configuration);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Name, nombre),
            new Claim(AuthClaimTypes.TipoUsuario, persona.Cliente is null ? AuthClaimTypes.Personal : AuthClaimTypes.Cliente)
        };

        foreach (var rol in roles)
            claims.Add(new Claim(ClaimTypes.Role, rol.ToString()));

        if (persona.Cliente is not null)
            claims.Add(new Claim(AuthClaimTypes.ClienteId, persona.Cliente.Id.ToString()));
        if (usuario.SedeId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.SedeId, usuario.SedeId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expirationHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
