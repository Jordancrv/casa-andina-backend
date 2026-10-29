using System.Text.Json;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Reservas.Queries;

public sealed record GetReservaDetalleQuery(int Id) : IRequest<DetalleReservaDto>;
public sealed record DetalleReservaDto(IReadOnlyList<DetalleHabitacionReservaDto> Habitaciones, IReadOnlyList<DetalleServicioReservaDto> Servicios);
public sealed record DetalleHabitacionReservaDto(int Id, string Numero, string Tipo, IReadOnlyList<DetalleHuespedReservaDto> Huespedes);
public sealed record DetalleHuespedReservaDto(bool EsTitular, DetalleHuespedDatos Datos, bool DatosHistoricosCompletos);
public sealed record DetalleServicioReservaDto(int Id, string Nombre, int Cantidad, decimal PrecioUnitario, decimal Subtotal);

public sealed class GetReservaDetalleQueryHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetReservaDetalleQuery, DetalleReservaDto>
{
    public async Task<DetalleReservaDto> Handle(GetReservaDetalleQuery request, CancellationToken ct)
    {
        var clienteId = user.ClienteId ?? throw new UnauthorizedAccessException("El token no identifica a un cliente.");
        var reserva = await db.Reservas.AsNoTracking().Where(r => r.Id == request.Id && r.ClienteId == clienteId)
            .Select(r => new {
                Habitaciones = r.Habitaciones.OrderBy(h => h.Habitacion.Numero).Select(h => new {
                    h.Id, h.Habitacion.Numero, Tipo = h.Habitacion.TipoHabitacion.Nombre,
                    Huespedes = h.Huespedes.OrderByDescending(g => g.EsTitular).ThenBy(g => g.HuespedId)
                        .Select(g => new { g.EsTitular, g.DatosHuespedJson, g.Huesped.PersonaNatural.Nombres, g.Huesped.PersonaNatural.Apellidos }).ToArray()
                }).ToArray(),
                Servicios = r.Servicios.OrderBy(s => s.Servicio.Nombre).Select(s => new DetalleServicioReservaDto(s.ServicioId, s.Servicio.Nombre, s.Cantidad, s.PrecioUnitario, s.Cantidad * s.PrecioUnitario)).ToArray()
            }).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Reserva", request.Id);
        var rooms = reserva.Habitaciones.Select(h => new DetalleHabitacionReservaDto(h.Id, h.Numero, h.Tipo,
            h.Huespedes.Select(g => {
                DetalleHuespedDatos? datos = null;
                if (!string.IsNullOrWhiteSpace(g.DatosHuespedJson))
                    try { datos = JsonSerializer.Deserialize<DetalleHuespedDatos>(g.DatosHuespedJson); } catch (JsonException) { /* registros antiguos o inválidos */ }
                return new DetalleHuespedReservaDto(g.EsTitular, datos ?? new(g.Nombres, g.Apellidos, null, null, null, null, null), datos != null);
            }).ToArray())).ToArray();
        return new(rooms, reserva.Servicios);
    }
}
