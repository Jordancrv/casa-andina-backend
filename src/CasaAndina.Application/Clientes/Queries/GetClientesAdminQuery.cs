using CasaAndina.Application.Clientes.DTOs;
using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Clientes.Queries;

public sealed record GetClientesAdminQuery(string? Buscar = null, int PageNumber = 1, int PageSize = 20,
    string? Estado = null, int? SedeId = null, string? Categoria = null, string? Actividad = null)
    : IRequest<ClientesCrmDto>;

public sealed class GetClientesAdminQueryValidator : AbstractValidator<GetClientesAdminQuery>
{
    public GetClientesAdminQueryValidator()
    {
        RuleFor(q => q.PageNumber).InclusiveBetween(1, 1000000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleFor(q => q.Buscar).MaximumLength(100);
        RuleFor(q => q.SedeId).GreaterThan(0).When(q => q.SedeId.HasValue);
        RuleFor(q => q.Estado).Must(v => v is null or "Activo" or "Inactivo");
        RuleFor(q => q.Categoria).Must(v => v is null or "Ocasional" or "Frecuente" or "VIP");
        RuleFor(q => q.Actividad).Must(v => v is null or "ConReservasActivas" or "NuevosMes");
    }
}

public sealed class GetClientesAdminQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<GetClientesAdminQuery, ClientesCrmDto>
{
    public async Task<ClientesCrmDto> Handle(GetClientesAdminQuery request, CancellationToken ct)
    {
        var sede = ClientesScope.Sede(currentUser, request.SedeId);
        var buscar = string.IsNullOrWhiteSpace(request.Buscar) ? null : request.Buscar.Trim();
        var hoy = PeruBusinessTime.Today;
        var mes = new DateTime(hoy.Year, hoy.Month, 1);
        var inicioMes = PeruBusinessTime.UtcStartOfDay(mes);
        var finMes = PeruBusinessTime.UtcStartOfDay(mes.AddMonths(1));
        var reservas = context.Reservas.AsNoTracking().Where(r => !sede.HasValue || r.SedeId == sede);
        var clientes = ClientesScope.Aplicar(context.Clientes.AsNoTracking(), sede)
            .Where(c => request.Estado == null || c.Estado == request.Estado)
            .Where(c => buscar == null
                || (c.Persona.Natural != null && (c.Persona.Natural.Nombres + " " + (c.Persona.Natural.Apellidos ?? "")).Contains(buscar))
                || (c.Persona.Juridica != null && (c.Persona.Juridica.RazonSocial.Contains(buscar) || (c.Persona.Juridica.NombreComercial != null && c.Persona.Juridica.NombreComercial.Contains(buscar))))
                || context.Usuarios.Any(u => u.PersonaId == c.PersonaId && u.Correo.Contains(buscar))
                || c.Persona.Contactos.Any(p => p.TipoContacto == "Correo" && p.Valor.Contains(buscar))
                || c.Persona.Documentos.Any(d => d.Numero.Contains(buscar)))
            .Select(c => new
            {
                Cliente = c,
                Completadas = reservas.Count(r => r.ClienteId == c.Id && r.Estado == "Completada"),
                Activas = reservas.Count(r => r.ClienteId == c.Id && (r.Estado == "Pendiente" || r.Estado == "Confirmada")
                    && r.Habitaciones.Any(h => h.FechaCheckOutReal == null && (h.FechaCheckOut > hoy || h.FechaCheckInReal != null))),
                Nuevo = c.FechaRegistro >= inicioMes && c.FechaRegistro < finMes
            })
            .Where(x => request.Categoria == null
                || (request.Categoria == "Ocasional" && x.Completadas <= 1)
                || (request.Categoria == "Frecuente" && x.Completadas >= 2 && x.Completadas <= 4)
                || (request.Categoria == "VIP" && x.Completadas >= 5))
            .Where(x => request.Actividad == null || (request.Actividad == "ConReservasActivas" && x.Activas > 0)
                || (request.Actividad == "NuevosMes" && x.Nuevo));

        // Todos los KPI usan los mismos filtros y alcance que la tabla, antes de paginar.
        var total = await clientes.CountAsync(ct);
        var activas = await clientes.CountAsync(x => x.Activas > 0, ct);
        var nuevos = await clientes.CountAsync(x => x.Nuevo, ct);
        var page = Math.Min(request.PageNumber, Math.Max(1, (int)Math.Ceiling(total / (double)request.PageSize)));
        var items = await clientes.OrderByDescending(x => x.Cliente.FechaRegistro).ThenByDescending(x => x.Cliente.Id).Skip((page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new ClienteAdminDto(
                x.Cliente.Id,
                x.Cliente.Persona.TipoPersona == "N"
                    ? (x.Cliente.Persona.Natural!.Nombres + " " + (x.Cliente.Persona.Natural.Apellidos ?? "")).Trim()
                    : (x.Cliente.Persona.Juridica!.NombreComercial ?? x.Cliente.Persona.Juridica.RazonSocial),
                x.Cliente.Persona.TipoPersona == "N" ? "Natural" : "Jurídica",
                context.Usuarios.Where(u => u.PersonaId == x.Cliente.PersonaId).Select(u => u.Correo).FirstOrDefault(),
                x.Cliente.Persona.Contactos.Where(p => p.TipoContacto == "Correo").OrderByDescending(p => p.EsPrincipal).ThenBy(p => p.Id).Select(p => p.Valor).FirstOrDefault(),
                reservas.Count(r => r.ClienteId == x.Cliente.Id),
                reservas.Where(r => r.ClienteId == x.Cliente.Id && r.Estado != "Cancelada").Select(r => (decimal?)r.PrecioTotal).Sum() ?? 0,
                context.DetallesPago.Where(d => d.Reserva.ClienteId == x.Cliente.Id && (!sede.HasValue || d.Reserva.SedeId == sede)
                    && d.Pago.EstadoPago.Codigo == "APROBADO").Select(d => (decimal?)d.MontoAplicado).Sum() ?? 0,
                reservas.Where(r => r.ClienteId == x.Cliente.Id && r.Habitaciones.Any()).OrderByDescending(r => r.FechaCreacion).ThenByDescending(r => r.Id)
                    .Select(r => new ReservaClienteAdminDto(r.Id, r.CodigoReserva, r.Estado, r.Habitaciones.Min(h => h.FechaCheckIn),
                        r.Habitaciones.Max(h => h.FechaCheckOut), r.PrecioTotal, r.Sede.Nombre)).ToArray(),
                x.Cliente.Estado, x.Cliente.FechaRegistro,
                x.Completadas >= 5 ? "VIP" : x.Completadas >= 2 ? "Frecuente" : "Ocasional", x.Completadas, x.Activas,
                x.Cliente.Persona.Documentos.OrderByDescending(d => d.EsPrincipal).ThenBy(d => d.Id).Select(d => d.TipoDocumento.Codigo).FirstOrDefault(),
                x.Cliente.Persona.Documentos.OrderByDescending(d => d.EsPrincipal).ThenBy(d => d.Id).Select(d => d.Numero).FirstOrDefault(),
                x.Cliente.Persona.Contactos.Where(p => p.TipoContacto == "Telefono").OrderByDescending(p => p.EsPrincipal).ThenBy(p => p.Id).Select(p => p.Valor).FirstOrDefault(),
                reservas.Where(r => r.ClienteId == x.Cliente.Id).OrderByDescending(r => r.FechaCreacion).ThenByDescending(r => r.Id).Select(r => r.Sede.Nombre).FirstOrDefault()
                    ?? (x.Cliente.SedeRegistro != null && (!sede.HasValue || x.Cliente.SedeRegistroId == sede) ? x.Cliente.SedeRegistro.Nombre : null),
                x.Cliente.SedeRegistroId,
                x.Cliente.Persona.Natural != null ? x.Cliente.Persona.Natural.Nombres : null,
                x.Cliente.Persona.Natural != null ? x.Cliente.Persona.Natural.Apellidos : null,
                x.Cliente.Persona.Juridica != null ? x.Cliente.Persona.Juridica.RazonSocial : null,
                x.Cliente.Persona.Juridica != null ? x.Cliente.Persona.Juridica.NombreComercial : null,
                x.Cliente.Persona.Documentos.OrderByDescending(d => d.EsPrincipal).ThenBy(d => d.Id).Select(d => (int?)d.TipoDocumentoId).FirstOrDefault()))
            .ToListAsync(ct);
        return new(items, total, page, request.PageSize, new(total, activas, nuevos));
    }
}
