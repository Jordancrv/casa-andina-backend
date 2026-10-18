namespace CasaAndina.Application.Servicios.DTOs;

public record ServicioDto(
    int Id,
    string Nombre,
    string? Descripcion,
    decimal Precio,
    IReadOnlyList<int> SedesIds,
    IReadOnlyList<string> Sedes,
    bool Activo);
