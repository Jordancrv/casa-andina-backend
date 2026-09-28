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
        => GenerarToken(
            usuario.Id,
            usuario.Email,
            usuario.NombreCompleto,
            usuario.Rol,
            AuthClaimTypes.Personal,
            usuario.SedeId);

    public string GenerarToken(Cliente cliente)
        => GenerarToken(
            cliente.Id,
            cliente.Correo,
            cliente.NombreCompleto,
            RolUsuario.Cliente,
            AuthClaimTypes.Cliente,
            null);

    private string GenerarToken(
        int id,
        string email,
        string nombre,
        RolUsuario rol,
        string tipoUsuario,
        int? sedeId)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Name, nombre),
            new Claim(ClaimTypes.Role, rol.ToString()),
            new Claim(AuthClaimTypes.TipoUsuario, tipoUsuario)
        };

        if (sedeId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.SedeId, sedeId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(
                double.Parse(jwtSettings["ExpirationHours"] ?? "8")),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
