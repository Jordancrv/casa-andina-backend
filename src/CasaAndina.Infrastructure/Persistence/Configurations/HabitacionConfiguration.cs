using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public class HabitacionConfiguration : IEntityTypeConfiguration<Habitacion>
{
    public void Configure(EntityTypeBuilder<Habitacion> builder)
    {
        builder.Property(h => h.Numero).HasMaxLength(10).IsRequired();
        builder.Property(h => h.PrecioNoche).HasColumnType("decimal(10,2)");

        builder.HasIndex(h => new { h.SedeId, h.Numero }).IsUnique();

        builder.HasOne(h => h.Sede)
            .WithMany(s => s.Habitaciones)
            .HasForeignKey(h => h.SedeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(h => h.Comodidades)
            .WithMany(c => c.Habitaciones)
            .UsingEntity(j => j.ToTable("HabitacionComodidad"));
    }
}
