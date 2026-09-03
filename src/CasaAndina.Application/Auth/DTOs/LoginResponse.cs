namespace CasaAndina.Application.Auth.DTOs;

public record LoginResponse(string Token, string NombreCompleto, string Rol);
