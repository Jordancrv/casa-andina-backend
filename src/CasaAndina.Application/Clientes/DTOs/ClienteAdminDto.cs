namespace CasaAndina.Application.Clientes.DTOs;

public sealed record ClienteAdminDto(
    int ClienteId,
    string Nombre,
    string TipoPersona,
    string? CorreoAcceso,
    string? CorreoNotificacion,
    int TotalReservas,
    decimal ImporteReservado,
    decimal ImportePagadoSimulado,
    IReadOnlyList<ReservaClienteAdminDto> Historial,
    string Estado,
    DateTime FechaRegistro,
    string Categoria,
    int EstadiasCompletadas,
    int ReservasActivas,
    string? DocumentoTipo,
    string? DocumentoNumero,
    string? Telefono,
    string? SedeReferencia,
    int? SedeRegistroId,
    string? Nombres,
    string? Apellidos,
    string? RazonSocial,
    string? NombreComercial,
    int? TipoDocumentoId);

public sealed record ClientesKpisDto(int TotalClientes, int ConReservasActivas, int NuevosEsteMes);

public sealed class ClientesCrmDto(IReadOnlyCollection<ClienteAdminDto> items, int count, int page, int size, ClientesKpisDto kpis)
    : CasaAndina.Application.Common.Models.PaginatedList<ClienteAdminDto>(items, count, page, size)
{
    public ClientesKpisDto Kpis { get; } = kpis;
}

public sealed record SedeCrmDto(int Id, string Nombre, string Ciudad);
public sealed record DocumentoCrmDto(int Id, string Codigo, string Nombre, string TipoPersonaPermitida, byte LongitudMinima, byte LongitudMaxima);
public sealed record CatalogosCrmDto(IReadOnlyList<SedeCrmDto> Sedes, IReadOnlyList<DocumentoCrmDto> TiposDocumento, bool PuedeElegirSede, int? SedeAsignadaId);

public sealed record ReservaClienteAdminDto(
    int ReservaId,
    string CodigoReserva,
    string Estado,
    DateTime FechaCheckIn,
    DateTime FechaCheckOut,
    decimal Importe,
    string Sede);
