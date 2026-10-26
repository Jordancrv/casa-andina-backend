using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.DTOs;
using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CasaAndina.Application.Reservas;

public static class ComprobanteEmision
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<ComprobanteDocumentoDto> CrearAsync(IApplicationDbContext context, Reserva reserva, bool reconstruido, CancellationToken ct)
    {
        var comprobante = reserva.Comprobante ?? throw new ConflictException("La reserva no tiene un comprobante registrado.");
        var pagos = await context.DetallesPago.AsNoTracking().Where(d => d.ReservaId == reserva.Id && d.Pago.EstadoPago.Codigo == "APROBADO" && d.Pago.Moneda == "PEN")
            .OrderBy(d => d.Pago.FechaCreacion).ThenBy(d => d.Id)
            .Select(d => new PagoComprobanteDto(d.Pago.MetodoPago.Nombre, d.Pago.FechaCreacion, d.MontoAplicado)).ToListAsync(ct);
        if (comprobante.MontoTotal <= 0 || comprobante.MontoTotal != reserva.PrecioTotal || pagos.Sum(p => p.MontoAplicado) < comprobante.MontoTotal)
            throw new ConflictException("El comprobante estará disponible cuando los pagos aprobados cubran el total de la reserva.");
        var cliente = await context.Clientes.AsNoTracking().Where(c => c.Id == reserva.ClienteId)
            .Select(c => new
            {
                Nombre = c.Persona.TipoPersona == "N" ? (c.Persona.Natural!.Nombres + " " + (c.Persona.Natural.Apellidos ?? "")).Trim() : c.Persona.Juridica!.RazonSocial,
                DocumentoTipo = c.Persona.Documentos.Where(d => comprobante.Tipo != Domain.Enums.TipoComprobante.Factura || d.TipoDocumento.Codigo == "RUC")
                    .OrderByDescending(d => d.EsPrincipal).ThenBy(d => d.Id).Select(d => d.TipoDocumento.Codigo).FirstOrDefault(),
                DocumentoNumero = c.Persona.Documentos.Where(d => comprobante.Tipo != Domain.Enums.TipoComprobante.Factura || d.TipoDocumento.Codigo == "RUC")
                    .OrderByDescending(d => d.EsPrincipal).ThenBy(d => d.Id).Select(d => d.Numero).FirstOrDefault()
            }).SingleAsync(ct);
        var sede = await context.Sedes.AsNoTracking().SingleAsync(s => s.Id == reserva.SedeId, ct);
        var habitaciones = await context.ReservaHabitaciones.AsNoTracking().Where(h => h.ReservaId == reserva.Id)
            .OrderBy(h => h.Habitacion.Numero).ThenBy(h => h.Id)
            .Select(h => new { h.Habitacion.Numero, Tipo = h.Habitacion.TipoHabitacion.Nombre, h.FechaCheckIn, h.FechaCheckOut, h.PrecioAcordado, h.Subtotal }).ToListAsync(ct);
        if (habitaciones.Count == 0) throw new ConflictException("La reserva no tiene detalles de alojamiento para generar el comprobante.");
        var lineas = habitaciones.Select(h => new LineaComprobanteDto($"Habitación {h.Numero} - {h.Tipo}", "noche", (h.FechaCheckOut - h.FechaCheckIn).Days, h.PrecioAcordado, h.Subtotal)).ToList();
        var servicios = await context.ReservaServicios.AsNoTracking().Where(s => s.ReservaId == reserva.Id).OrderBy(s => s.ServicioId)
            .Select(s => new LineaComprobanteDto(s.Servicio.Nombre, "unidad", s.Cantidad, s.PrecioUnitario, s.Cantidad * s.PrecioUnitario)).ToListAsync(ct);
        lineas.AddRange(servicios);
        var result = new ComprobanteDocumentoDto(1, comprobante.NumeroComprobante, comprobante.Tipo.ToString(), reserva.CodigoReserva,
            comprobante.FechaEmision, sede.Nombre, sede.Ciudad, sede.Direccion, sede.Telefono, cliente.Nombre, cliente.DocumentoTipo,
            cliente.DocumentoNumero, habitaciones.Min(h => h.FechaCheckIn), habitaciones.Max(h => h.FechaCheckOut), lineas, pagos,
            comprobante.MontoTotal, "PEN", true, reconstruido);
        Validar(result, reserva);
        return result;
    }

    public static ComprobanteDocumentoDto Leer(Reserva reserva)
    {
        try
        {
            var result = JsonSerializer.Deserialize<ComprobanteDocumentoDto>(reserva.Comprobante!.DatosEmisionJson!, JsonOptions)
                ?? throw new JsonException();
            Validar(result, reserva);
            return result;
        }
        catch (JsonException) { throw new ConflictException("No se pudieron validar los datos de emisión del comprobante."); }
    }

    private static void Validar(ComprobanteDocumentoDto d, Reserva r)
    {
        if (d.Version != 1 || d.Numero != r.Comprobante!.NumeroComprobante || d.Tipo != r.Comprobante.Tipo.ToString()
            || d.CodigoReserva != r.CodigoReserva || d.Total <= 0 || d.Total != r.Comprobante.MontoTotal || d.Total != r.PrecioTotal
            || d.Moneda != "PEN" || !d.PagoSimulado || string.IsNullOrWhiteSpace(d.ClienteNombre)
            || d.Lineas is null || d.Lineas.Count == 0 || d.Lineas.Any(l => l is null || l.Cantidad <= 0 || l.Importe < 0 || l.PrecioUnitario < 0)
            || d.Lineas.Sum(l => l.Importe) != d.Total || d.Pagos is null || d.Pagos.Any(p => p is null || p.MontoAplicado <= 0)
            || d.Pagos.Sum(p => p.MontoAplicado) < d.Total)
            throw new ConflictException("Los datos del comprobante no coinciden con los importes registrados de la reserva.");
    }
}
