using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public class HabitacionImagenConfiguration : IEntityTypeConfiguration<HabitacionImagen>
{
    public void Configure(EntityTypeBuilder<HabitacionImagen> builder)
    {
        builder.ToTable("HabitacionImagen");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("HabitacionImagenId");
        builder.Property(x => x.Url).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.HabitacionId, x.Orden }).IsUnique();
        builder.HasOne(x => x.Habitacion).WithMany(x => x.Imagenes)
            .HasForeignKey(x => x.HabitacionId).OnDelete(DeleteBehavior.Cascade);
    }
}
