using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public class SedeConfiguration : IEntityTypeConfiguration<Sede>
{
    public void Configure(EntityTypeBuilder<Sede> builder)
    {
        builder.Property(s => s.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Ciudad).HasMaxLength(100).IsRequired();

        builder.HasMany(s => s.Servicios)
            .WithMany(sv => sv.Sedes)
            .UsingEntity(j => j.ToTable("SedeServicio"));

        // RF04: Sede es de solo lectura desde la API — no se le agrega ningún
        // ValueGeneratedOnAdd especial ni CRUD; se puebla vía script SQL (ver README).
    }
}
