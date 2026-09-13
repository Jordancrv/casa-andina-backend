using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public class HabitacionConfiguration : IEntityTypeConfiguration<Habitacion>
{
    public void Configure(EntityTypeBuilder<Habitacion> builder)
    {
        builder.ToTable("Habitacion");
        builder.Property(h => h.Id).HasColumnName("HabitacionId");

        builder.Property(h => h.Numero).HasMaxLength(10).IsRequired();

        // Columna Piso es TINYINT (byte) en BD SQL Server
        builder.Property(h => h.Piso).HasColumnType("tinyint");

        // PrecioNoche en dominio → PrecioBase en BD
        builder.Property(h => h.PrecioNoche)
            .HasColumnName("PrecioBase")
            .HasColumnType("decimal(10,2)");

        builder.Property(h => h.Estado).HasMaxLength(20);
        builder.Property(h => h.FotoUrl).HasMaxLength(500);
        builder.Property(h => h.TipoHabitacionId);

        // BaseEntity.FechaActualizacion no existe en la tabla Habitacion
        builder.Ignore(h => h.FechaActualizacion);

        builder.HasIndex(h => new { h.SedeId, h.Numero }).IsUnique();

        builder.HasOne(h => h.Sede)
            .WithMany(s => s.Habitaciones)
            .HasForeignKey(h => h.SedeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.TipoHabitacion)
            .WithMany()
            .HasForeignKey(h => h.TipoHabitacionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(h => h.Comodidades)
            .WithMany(c => c.Habitaciones)
            .UsingEntity<Dictionary<string, object>>(
                "HabitacionComodidad",
                j => j.HasOne<Comodidad>().WithMany().HasForeignKey("ComodidadId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Habitacion>().WithMany().HasForeignKey("HabitacionId").OnDelete(DeleteBehavior.Cascade)
            );
    }
}
