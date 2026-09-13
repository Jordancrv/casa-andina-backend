using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

/// <summary>
/// Comodidad: BD tiene solo ComodidadId y Nombre.
/// NO hereda BaseEntity → no tiene FechaCreacion/Activo/FechaActualizacion.
/// </summary>
public class ComodidadConfiguration : IEntityTypeConfiguration<Comodidad>
{
    public void Configure(EntityTypeBuilder<Comodidad> builder)
    {
        builder.ToTable("Comodidad");
        builder.Property(c => c.Id).HasColumnName("ComodidadId");
        builder.Property(c => c.Nombre).HasMaxLength(60).IsRequired();
        builder.HasIndex(c => c.Nombre).IsUnique();
    }
}

/// <summary>
/// Servicio: BD tiene ServicioId, Nombre, Descripcion, Precio, Activo, FechaCreacion.
/// FechaActualizacion de BaseEntity no existe en la BD → Ignore.
/// </summary>
public class ServicioConfiguration : IEntityTypeConfiguration<Servicio>
{
    public void Configure(EntityTypeBuilder<Servicio> builder)
    {
        builder.ToTable("Servicio");
        builder.Property(s => s.Id).HasColumnName("ServicioId");
        builder.Property(s => s.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Descripcion).HasMaxLength(300);
        builder.Property(s => s.Precio).HasColumnType("decimal(10,2)");

        builder.Ignore(s => s.FechaActualizacion);
    }
}
