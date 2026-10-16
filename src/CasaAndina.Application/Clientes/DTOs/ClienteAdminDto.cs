namespace CasaAndina.Application.Clientes.DTOs;

public sealed record ClienteAdminDto(
    int ClienteId,
    string Nombre,
    string TipoPersona,
    string? Correo,
    int TotalReservas,
    decimal ImporteReservado,
    decimal ImportePagadoSimulado,
    IReadOnlyList<ReservaClienteAdminDto> Historial);

public sealed record ReservaClienteAdminDto(
    int ReservaId,
    string CodigoReserva,
    string Estado,
    DateTime FechaCheckIn,
    DateTime FechaCheckOut,
    decimal Importe,
    string Sede);
