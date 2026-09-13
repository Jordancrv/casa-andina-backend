namespace CasaAndina.Application.Catalogos.DTOs;

public record TipoHabitacionDto(
    int Id,
    string Nombre,
    string? Descripcion,
    byte CapacidadAdultos,
    byte CapacidadNinos);

public record ComodidadDto(
    int Id,
    string Nombre);

public record SedeDto(
    int Id,
    string Nombre,
    string Ciudad);

public record CatalogosResponse(
    IReadOnlyList<TipoHabitacionDto> TiposHabitacion,
    IReadOnlyList<ComodidadDto> Comodidades,
    IReadOnlyList<SedeDto> Sedes);
