namespace CasaAndina.Application.Reservas.DTOs;

public sealed record HabitacionDisponibleDto(
    int HabitacionId,
    int SedeId,
    string SedeNombre,
    string Ciudad,
    string Numero,
    int Piso,
    string TipoHabitacion,
    int CapacidadAdultos,
    int CapacidadNinos,
    decimal PrecioNoche,
    IReadOnlyList<string> Comodidades,
    string? FotoUrl = null);

public sealed record HabitacionCalendarioDto(
    int HabitacionId,
    string Numero,
    int Piso,
    string TipoHabitacion,
    string EstadoHabitacion,
    IReadOnlyList<ReservaCalendarioDto> Reservas);

public sealed record ReservaCalendarioDto(
    DateTime FechaCheckIn,
    DateTime FechaCheckOut,
    string Estado,
    string? NombreReservante);

public sealed record ServicioDisponibleDto(int Id, string Nombre, string? Descripcion, decimal Precio);

public sealed record HuespedCompraDto(
    string Nombres,
    string Apellidos,
    string Correo,
    string? Telefono,
    bool EsTitular,
    string? Dni = null,
    DateTime? FechaNacimiento = null,
    string? Sexo = null);

public sealed record HabitacionCompraDto(
    int HabitacionId,
    IReadOnlyList<HuespedCompraDto> Huespedes);

public sealed record ServicioCompraDto(int ServicioId, int Cantidad);

public sealed record DetalleHuespedDatos(string Nombres, string? Apellidos, string? Dni,
    DateTime? FechaNacimiento, string? Sexo, string? Correo, string? Telefono);

public sealed record CrearReservaCompraCommand(
    int SedeId,
    DateTime FechaCheckIn,
    DateTime FechaCheckOut,
    IReadOnlyList<HabitacionCompraDto> Habitaciones,
    IReadOnlyList<ServicioCompraDto> Servicios,
    string TipoComprobante,
    string? RazonSocial,
    string? Ruc,
    string MetodoPagoCodigo) : MediatR.IRequest<ReservaCompraResponseDto>;

public sealed record ReservaCompraResponseDto(
    int ReservaId,
    string CodigoReserva,
    string Estado,
    decimal PrecioTotal,
    string TipoComprobante,
    string MetodoPago,
    string EstadoPago,
    string MensajePago,
    string? NumeroComprobante = null,
    bool ComprobanteDisponible = false);

public sealed record MetodoPagoDto(string Codigo, string Nombre, string Categoria);

public sealed record PagoReservaDto(
    int PagoId,
    string MetodoPago,
    string Estado,
    decimal Importe,
    string Moneda,
    DateTime FechaCreacion,
    decimal MontoAplicado);
