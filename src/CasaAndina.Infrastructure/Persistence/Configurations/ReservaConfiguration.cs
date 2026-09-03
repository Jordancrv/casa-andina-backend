using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public class ReservaConfiguration : IEntityTypeConfiguration<Reserva>
{
    public void Configure(EntityTypeBuilder<Reserva> builder)
    {
        builder.Property(r => r.Codigo).HasMaxLength(10).IsRequired();
        builder.HasIndex(r => r.Codigo).IsUnique();
        builder.Property(r => r.MontoTotal).HasColumnType("decimal(10,2)");
        builder.Property(r => r.Estado).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(r => r.Usuario)
            .WithMany(u => u.Reservas)
            .HasForeignKey(r => r.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReservaHabitacionConfiguration : IEntityTypeConfiguration<ReservaHabitacion>
{
    public void Configure(EntityTypeBuilder<ReservaHabitacion> builder)
    {
        builder.HasKey(rh => new { rh.ReservaId, rh.HabitacionId });
    }
}
