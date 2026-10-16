using CasaAndina.Application.Clientes.DTOs;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common.Models;
using CasaAndina.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Clientes.Queries;

public sealed record GetClientesAdminQuery(string? Buscar = null, int PageNumber = 1, int PageSize = 20)
    : IRequest<PaginatedList<ClienteAdminDto>>;

public sealed class GetClientesAdminQueryValidator : AbstractValidator<GetClientesAdminQuery>
{
    public GetClientesAdminQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Buscar).MaximumLength(100);
    }
}

public sealed class GetClientesAdminQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetClientesAdminQuery, PaginatedList<ClienteAdminDto>>
{
    public async Task<PaginatedList<ClienteAdminDto>> Handle(
        GetClientesAdminQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Rol is not (RolUsuario.Administrador or RolUsuario.Superadministrador))
            throw new ForbiddenAccessException("No tiene permisos para consultar el directorio de clientes.");
        if (currentUser.Rol != RolUsuario.Superadministrador && !currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("La cuenta administrativa no tiene una sede asignada.");

        var buscar = string.IsNullOrWhiteSpace(request.Buscar) ? null : request.Buscar.Trim();
        var reservas = context.Reservas.AsNoTracking();
        if (currentUser.SedeId.HasValue)
            reservas = reservas.Where(reserva => reserva.SedeId == currentUser.SedeId.Value);

        var clientes = context.Clientes.AsNoTracking()
            .Where(cliente => cliente.Estado == "Activo")
            .Where(cliente => !currentUser.SedeId.HasValue || reservas.Any(reserva => reserva.ClienteId == cliente.Id))
            .Where(cliente => buscar == null
                || (cliente.Persona.Natural != null &&
                    (cliente.Persona.Natural.Nombres.Contains(buscar)
                     || (cliente.Persona.Natural.Apellidos != null && cliente.Persona.Natural.Apellidos.Contains(buscar))))
                || (cliente.Persona.Juridica != null &&
                    (cliente.Persona.Juridica.RazonSocial.Contains(buscar)
                     || (cliente.Persona.Juridica.NombreComercial != null && cliente.Persona.Juridica.NombreComercial.Contains(buscar))))
                || cliente.Persona.Contactos.Any(contacto => contacto.TipoContacto == "Correo" && contacto.Valor.Contains(buscar)))
            .OrderBy(cliente => cliente.Id);

        var projection = clientes.Select(cliente => new ClienteAdminDto(
            cliente.Id,
            cliente.Persona.TipoPersona == "N"
                ? ((cliente.Persona.Natural!.Nombres + " ") + (cliente.Persona.Natural.Apellidos ?? "")).Trim()
                : (cliente.Persona.Juridica!.NombreComercial ?? cliente.Persona.Juridica.RazonSocial),
            cliente.Persona.TipoPersona == "N" ? "Natural" : "Jurídica",
            cliente.Persona.Contactos
                .Where(contacto => contacto.TipoContacto == "Correo")
                .OrderByDescending(contacto => contacto.EsPrincipal)
                .Select(contacto => contacto.Valor)
                .FirstOrDefault(),
            reservas.Count(reserva => reserva.ClienteId == cliente.Id),
            reservas.Where(reserva => reserva.ClienteId == cliente.Id && reserva.Estado != "Cancelada")
                .Select(reserva => (decimal?)reserva.PrecioTotal).Sum() ?? 0,
            reservas.Where(reserva => reserva.ClienteId == cliente.Id)
                .Where(reserva => reserva.Habitaciones.Any())
                .OrderByDescending(reserva => reserva.FechaCreacion)
                .Select(reserva => new ReservaClienteAdminDto(
                    reserva.Id,
                    reserva.CodigoReserva,
                    reserva.Estado,
                    reserva.Habitaciones.Min(detalle => detalle.FechaCheckIn),
                    reserva.Habitaciones.Max(detalle => detalle.FechaCheckOut),
                    reserva.PrecioTotal,
                    reserva.Sede.Nombre))
                .ToArray()));

        return await PaginatedList<ClienteAdminDto>.CreateAsync(
            projection, request.PageNumber, request.PageSize, cancellationToken);
    }
}
