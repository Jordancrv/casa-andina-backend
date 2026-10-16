using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Common.Models;
using CasaAndina.Application.Usuarios.DTOs;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Usuarios.Queries;

public sealed record GetUsuariosAdminQuery(string? Buscar = null, int PageNumber = 1, int PageSize = 20)
    : IRequest<PaginatedList<UsuarioAdminDto>>;

public sealed class GetUsuariosAdminQueryValidator : AbstractValidator<GetUsuariosAdminQuery>
{
    public GetUsuariosAdminQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Buscar).MaximumLength(100);
    }
}

public sealed record GetUsuarioAdminByIdQuery(int Id) : IRequest<UsuarioAdminDto>;

public sealed class GetUsuariosAdminQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<GetUsuariosAdminQuery, PaginatedList<UsuarioAdminDto>>,
      IRequestHandler<GetUsuarioAdminByIdQuery, UsuarioAdminDto>
{
    public async Task<PaginatedList<UsuarioAdminDto>> Handle(
        GetUsuariosAdminQuery request, CancellationToken cancellationToken)
    {
        EnsureAdministrator();
        EnsureSedeScope();
        var query = BuildQuery(request.Buscar);
        var projection = Project(query);
        return await PaginatedList<UsuarioAdminDto>.CreateAsync(
            projection, request.PageNumber, request.PageSize, cancellationToken);
    }

    public async Task<UsuarioAdminDto> Handle(
        GetUsuarioAdminByIdQuery request, CancellationToken cancellationToken)
    {
        EnsureAdministrator();
        EnsureSedeScope();
        var usuario = await Project(BuildQuery(null).Where(item => item.Id == request.Id))
            .SingleOrDefaultAsync(cancellationToken);
        return usuario ?? throw new NotFoundException("Usuario", request.Id);
    }

    private IQueryable<Usuario> BuildQuery(string? search)
    {
        var users = context.Usuarios.AsNoTracking();
        if (currentUser.SedeId.HasValue)
        {
            var sedeId = currentUser.SedeId.Value;
            users = users.Where(user => user.SedeId == sedeId ||
                context.Clientes.Any(cliente => cliente.PersonaId == user.PersonaId &&
                    cliente.Reservas.Any(reserva => reserva.SedeId == sedeId)));
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (normalizedSearch is not null)
        {
            users = users.Where(user =>
                (user.Persona.Natural != null &&
                    (user.Persona.Natural.Nombres.Contains(normalizedSearch) ||
                     (user.Persona.Natural.Apellidos != null && user.Persona.Natural.Apellidos.Contains(normalizedSearch)))) ||
                (user.Persona.Juridica != null &&
                    (user.Persona.Juridica.RazonSocial.Contains(normalizedSearch) ||
                     (user.Persona.Juridica.NombreComercial != null && user.Persona.Juridica.NombreComercial.Contains(normalizedSearch)))) ||
                user.Correo.Contains(normalizedSearch));
        }

        return users.OrderBy(user => user.PersonaId);
    }

    private static IQueryable<UsuarioAdminDto> Project(IQueryable<Usuario> query)
        => query.Select(user => new UsuarioAdminDto(
            user.Id,
            user.Persona.TipoPersona == "N"
                ? ((user.Persona.Natural!.Nombres + " ") + (user.Persona.Natural.Apellidos ?? "")).Trim()
                : (user.Persona.Juridica!.NombreComercial ?? user.Persona.Juridica.RazonSocial),
            user.Correo,
            user.Persona.Contactos
                .Where(contacto => contacto.TipoContacto == "Telefono")
                .OrderByDescending(contacto => contacto.EsPrincipal)
                .Select(contacto => contacto.Valor)
                .FirstOrDefault(),
            user.Persona.TipoPersona == "N" ? "Natural" : "Jurídica",
            user.SedeId,
            user.Sede != null ? user.Sede.Nombre : null,
            user.FechaCreacion,
            user.Roles.OrderBy(usuarioRol => usuarioRol.Rol.Nombre)
                .Select(usuarioRol => usuarioRol.Rol.Nombre).ToArray(),
            user.Activo));

    private void EnsureAdministrator()
    {
        if (currentUser.Rol is not (RolUsuario.Administrador or RolUsuario.Superadministrador))
            throw new ForbiddenAccessException("No tiene permisos para consultar usuarios.");
    }

    private void EnsureSedeScope()
    {
        if (currentUser.Rol != RolUsuario.Superadministrador && !currentUser.SedeId.HasValue)
            throw new ForbiddenAccessException("La cuenta administrativa no tiene una sede asignada.");
    }
}
