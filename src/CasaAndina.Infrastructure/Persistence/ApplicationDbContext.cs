using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Sede> Sedes => Set<Sede>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();
    public DbSet<TipoHabitacion> TiposHabitacion => Set<TipoHabitacion>();
    public DbSet<Habitacion> Habitaciones => Set<Habitacion>();
    public DbSet<Comodidad> Comodidades => Set<Comodidad>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<PersonaNatural> PersonasNaturales => Set<PersonaNatural>();
    public DbSet<PersonaJuridica> PersonasJuridicas => Set<PersonaJuridica>();
    public DbSet<TipoDocumento> TiposDocumento => Set<TipoDocumento>();
    public DbSet<PersonaDocumento> PersonaDocumentos => Set<PersonaDocumento>();
    public DbSet<PersonaContacto> PersonaContactos => Set<PersonaContacto>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<ReservaHabitacion> ReservaHabitaciones => Set<ReservaHabitacion>();
    public DbSet<ReservaServicio> ReservaServicios => Set<ReservaServicio>();
    public DbSet<Comprobante> Comprobantes => Set<Comprobante>();
    public DbSet<Huesped> Huespedes => Set<Huesped>();
    public DbSet<AsignacionHuesped> AsignacionesHuespedes => Set<AsignacionHuesped>();
    public DbSet<Auditoria> Auditoria => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(builder);
    }
}
