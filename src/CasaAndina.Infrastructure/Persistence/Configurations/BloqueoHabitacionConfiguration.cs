using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public sealed class BloqueoHabitacionConfiguration : IEntityTypeConfiguration<BloqueoHabitacion>
{
    public void Configure(EntityTypeBuilder<BloqueoHabitacion> b)
    {
        b.ToTable("BloqueoHabitacion"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("BloqueoHabitacionId");
        b.Property(x => x.Inicio).HasColumnType("date"); b.Property(x => x.FinExclusivo).HasColumnType("date");
        b.Property(x => x.Motivo).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Habitacion).WithMany().HasForeignKey(x => x.HabitacionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.HabitacionId, x.Inicio, x.FinExclusivo });
    }
}
