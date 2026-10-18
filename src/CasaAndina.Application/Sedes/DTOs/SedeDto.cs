namespace CasaAndina.Application.Sedes.DTOs;

public sealed record SedeDto(
    int Id,
    string Codigo,
    string Nombre,
    string Region,
    string Ciudad,
    string? Direccion,
    string Categoria,
    string? Telefono);
