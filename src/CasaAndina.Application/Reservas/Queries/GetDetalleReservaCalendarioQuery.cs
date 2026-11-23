using System.Text.Json;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.DTOs;
using CasaAndina.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Reservas.Queries;

public sealed record GetDetalleReservaCalendarioQuery(int Id) : IRequest<DetalleReservaCalendarioDto>;
public sealed record DetalleReservaCalendarioDto(string Codigo, string Estado, string Canal, string Titular, string Sede, decimal Total,
    IReadOnlyList<HabitacionDetalleCalendarioDto> Habitaciones, IReadOnlyList<DetalleServicioReservaDto> Servicios,
    IReadOnlyList<PagoDetalleCalendarioDto> Pagos);
public sealed record HabitacionDetalleCalendarioDto(string Numero, string Tipo, DateTime Entrada, DateTime Salida,
    decimal Subtotal, IReadOnlyList<DetalleHuespedReservaDto> Huespedes);
public sealed record PagoDetalleCalendarioDto(string Metodo, string Estado, decimal Monto, string Moneda);

public sealed class GetDetalleReservaCalendarioQueryHandler(IApplicationDbContext db, ICurrentUserService user)
    : IRequestHandler<GetDetalleReservaCalendarioQuery, DetalleReservaCalendarioDto>
{
    public async Task<DetalleReservaCalendarioDto> Handle(GetDetalleReservaCalendarioQuery request, CancellationToken ct)
    {
        if (user.Rol != RolUsuario.Administrador || !user.SedeId.HasValue || !user.TienePermiso("calendario.ver"))
            throw new ForbiddenAccessException("No tiene permisos para consultar el detalle de reservas.");
        var r = await db.Reservas.AsNoTracking().Where(r => r.Id == request.Id && r.SedeId == user.SedeId)
            .Select(r => new {
                Codigo = r.CodigoReserva, r.Estado, r.Canal, Sede = r.Sede.Nombre, Total = r.PrecioTotal,
                Titular = r.Cliente.Persona.TipoPersona == "N"
                    ? (r.Cliente.Persona.Natural!.Nombres + " " + (r.Cliente.Persona.Natural.Apellidos ?? "")).Trim()
                    : r.Cliente.Persona.Juridica!.RazonSocial,
                Habitaciones = r.Habitaciones.OrderBy(h => h.Habitacion.Numero).Select(h => new {
                    h.Habitacion.Numero, Tipo = h.Habitacion.TipoHabitacion.Nombre, Entrada = h.FechaCheckIn, Salida = h.FechaCheckOut, h.Subtotal,
                    Huespedes = h.Huespedes.OrderByDescending(g => g.EsTitular).Select(g => new {
                        g.EsTitular, g.DatosHuespedJson, g.Huesped.PersonaNatural.Nombres, g.Huesped.PersonaNatural.Apellidos
                    }).ToArray()
                }).ToArray(),
                Servicios = r.Servicios.Select(s => new DetalleServicioReservaDto(s.ServicioId, s.Servicio.Nombre, s.Cantidad, s.PrecioUnitario, s.Subtotal)).ToArray(),
                Pagos = r.Pagos.Select(p => new PagoDetalleCalendarioDto(p.Pago.MetodoPago.Nombre, p.Pago.EstadoPago.Nombre, p.MontoAplicado, p.Pago.Moneda)).ToArray()
            }).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Reserva", request.Id);
        var habitaciones = r.Habitaciones.Select(h => new HabitacionDetalleCalendarioDto(h.Numero, h.Tipo, h.Entrada, h.Salida, h.Subtotal,
            h.Huespedes.Select(g => {
                DetalleHuespedDatos? datos = null;
                if (!string.IsNullOrWhiteSpace(g.DatosHuespedJson))
                    try { datos = JsonSerializer.Deserialize<DetalleHuespedDatos>(g.DatosHuespedJson); } catch (JsonException) { }
                return new DetalleHuespedReservaDto(g.EsTitular, datos ?? new(g.Nombres, g.Apellidos, null, null, null, null, null), datos != null);
            }).ToArray())).ToArray();
        return new(r.Codigo, r.Estado, r.Canal, r.Titular, r.Sede, r.Total, habitaciones, r.Servicios, r.Pagos);
    }
}
