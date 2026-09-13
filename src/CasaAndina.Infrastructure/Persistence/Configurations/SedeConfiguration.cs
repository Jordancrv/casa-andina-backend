using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public class SedeConfiguration : IEntityTypeConfiguration<Sede>
{
    public void Configure(EntityTypeBuilder<Sede> builder)
    {
        builder.ToTable("Sede");
        builder.Property(s => s.Id).HasColumnName("SedeId");

        builder.Property(s => s.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Region).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Ciudad).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Direccion).HasMaxLength(250);
        builder.Property(s => s.Categoria).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Telefono).HasMaxLength(20);

        // BaseEntity.FechaActualizacion no existe en la tabla Sede
        builder.Ignore(s => s.FechaActualizacion);

        builder.HasIndex(s => s.Codigo).IsUnique();

        // ServicioSede es la tabla puente (no SedeServicio)
        builder.HasMany(s => s.Servicios)
            .WithMany(sv => sv.Sedes)
            .UsingEntity(j => j.ToTable("ServicioSede"));

        // RF04: Sede es de solo lectura desde la API.
    }
}
