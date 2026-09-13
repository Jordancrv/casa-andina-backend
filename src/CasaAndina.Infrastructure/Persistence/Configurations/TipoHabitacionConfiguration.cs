using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public class TipoHabitacionConfiguration : IEntityTypeConfiguration<TipoHabitacion>
{
    public void Configure(EntityTypeBuilder<TipoHabitacion> builder)
    {
        builder.ToTable("TipoHabitacion");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("TipoHabitacionId");

        builder.Property(t => t.Nombre).HasMaxLength(60).IsRequired();
        builder.Property(t => t.Descripcion).HasMaxLength(250);
        builder.Property(t => t.CapacidadAdultos).HasColumnType("tinyint").HasDefaultValue((byte)2);
        builder.Property(t => t.CapacidadNinos).HasColumnType("tinyint").HasDefaultValue((byte)0);
    }
}
