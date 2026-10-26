namespace CasaAndina.Application.Reservas.DTOs;

public sealed record ComprobanteDocumentoDto(
    int Version, string Numero, string Tipo, string CodigoReserva, DateTime FechaEmision,
    string SedeNombre, string Ciudad, string? Direccion, string? TelefonoSede,
    string ClienteNombre, string? DocumentoTipo, string? DocumentoNumero,
    DateTime FechaEntrada, DateTime FechaSalida,
    IReadOnlyList<LineaComprobanteDto> Lineas,
    IReadOnlyList<PagoComprobanteDto> Pagos,
    decimal Total, string Moneda, bool PagoSimulado, bool DetalleReconstruido);

public sealed record LineaComprobanteDto(string Descripcion, string Unidad, int Cantidad, decimal PrecioUnitario, decimal Importe);
public sealed record PagoComprobanteDto(string Metodo, DateTime Fecha, decimal MontoAplicado);
public sealed record ComprobantePdfDto(byte[] Contenido, string NombreArchivo);
