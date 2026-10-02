using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Common.Interfaces;

/// <summary>
/// Application NO conoce EF Core como implementación, solo este contrato.
/// Infrastructure implementa esto con ApplicationDbContext (DbContext real).
/// Así los Handlers son testeables con un fake/InMemory sin levantar SQL Server.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Sede> Sedes { get; }
    DbSet<Rol> Roles { get; }
    DbSet<TipoHabitacion> TiposHabitacion { get; }
    DbSet<Habitacion> Habitaciones { get; }
    DbSet<Comodidad> Comodidades { get; }
    DbSet<Servicio> Servicios { get; }
    DbSet<Usuario> Usuarios { get; }
    DbSet<Cliente> Clientes { get; }
    DbSet<Persona> Personas { get; }
    DbSet<PersonaNatural> PersonasNaturales { get; }
    DbSet<PersonaJuridica> PersonasJuridicas { get; }
    DbSet<TipoDocumento> TiposDocumento { get; }
    DbSet<PersonaDocumento> PersonaDocumentos { get; }
    DbSet<PersonaCorreo> PersonaCorreos { get; }
    DbSet<PersonaTelefono> PersonaTelefonos { get; }
    DbSet<Reserva> Reservas { get; }
    DbSet<ReservaHuesped> ReservaHuespedes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
