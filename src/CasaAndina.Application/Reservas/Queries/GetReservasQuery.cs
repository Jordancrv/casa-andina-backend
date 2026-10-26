using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Reservas.Queries;

/// <summary>
/// RF03: lista únicamente las reservas del cliente autenticado.
/// </summary>
public record GetReservasQuery : IRequest<IReadOnlyList<ReservaDto>>;

public class GetReservasQueryHandler
    : IRequestHandler<GetReservasQuery, IReadOnlyList<ReservaDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetReservasQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ReservaDto>> Handle(
        GetReservasQuery request, CancellationToken cancellationToken)
    {
        var clienteId = _currentUser.ClienteId
            ?? throw new UnauthorizedAccessException("El token no identifica a un cliente.");

        return await _context.Reservas
            .AsNoTracking()
            .Where(r => r.ClienteId == clienteId)
            .Where(r => r.Habitaciones.Any())
            .OrderByDescending(r => r.Habitaciones.Min(detalle => detalle.FechaCheckIn))
            .Select(r => new ReservaDto(
                r.Id,
                r.CodigoReserva,
                r.Habitaciones.Min(detalle => detalle.FechaCheckIn),
                r.Habitaciones.Max(detalle => detalle.FechaCheckOut),
                r.PrecioTotal,
                r.Estado,
                r.Sede.Nombre,
                r.Sede.Ciudad,
                r.Habitaciones.OrderBy(detalle => detalle.FechaCheckIn)
                    .Select(detalle => detalle.Habitacion.Numero).ToArray(),
                r.Pagos.OrderBy(detalle => detalle.Pago.FechaCreacion)
                    .Select(detalle => new PagoReservaDto(
                        detalle.PagoId,
                        detalle.Pago.MetodoPago.Nombre,
                        detalle.Pago.EstadoPago.Nombre,
                        detalle.Pago.Importe,
                        detalle.Pago.Moneda,
                        detalle.Pago.FechaCreacion,
                        detalle.MontoAplicado)).ToArray(),
                r.Estado == "Cancelada" ? "Reserva cancelada" :
                r.Estado == "Completada" || r.Habitaciones.All(x => x.FechaCheckOutReal != null) ? "Estadía finalizada" :
                r.Habitaciones.Any(x => x.FechaCheckInReal != null && x.FechaCheckOutReal == null) ? "Estadía en curso" :
                r.Estado == "Confirmada" ? "Reserva confirmada" : r.Estado,
                r.Comprobante != null && r.Comprobante.MontoTotal > 0 && r.Comprobante.MontoTotal == r.PrecioTotal
                    && (r.Pagos.Where(p => p.Pago.EstadoPago.Codigo == "APROBADO" && p.Pago.Moneda == "PEN")
                        .Select(p => (decimal?)p.MontoAplicado).Sum() ?? 0) >= r.Comprobante.MontoTotal,
                r.Comprobante != null ? r.Comprobante.Tipo.ToString() : null,
                r.Comprobante != null ? r.Comprobante.NumeroComprobante : null))
            .ToListAsync(cancellationToken);
    }
}
