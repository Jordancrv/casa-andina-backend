using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;

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
    DbSet<UsuarioRol> UsuarioRoles { get; }
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
    DbSet<PersonaContacto> PersonaContactos { get; }
    DbSet<Reserva> Reservas { get; }
    DbSet<ReporteAnalisis> ReportesAnalisis { get; }
    DbSet<ReservaHabitacion> ReservaHabitaciones { get; }
    DbSet<ReservaServicio> ReservaServicios { get; }
    DbSet<Comprobante> Comprobantes { get; }
    DbSet<Huesped> Huespedes { get; }
    DbSet<AsignacionHuesped> AsignacionesHuespedes { get; }
    DbSet<Auditoria> Auditoria { get; }
    DbSet<EstadoPago> EstadosPago { get; }
    DbSet<MetodoPago> MetodosPago { get; }
    DbSet<Pago> Pagos { get; }
    DbSet<DetallePago> DetallesPago { get; }

    Task<IApplicationTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
