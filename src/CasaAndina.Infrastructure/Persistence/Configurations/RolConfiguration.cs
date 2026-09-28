using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public sealed class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("Rol");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("RolId");
        builder.Property(r => r.Codigo).HasMaxLength(30).IsRequired();
        builder.Property(r => r.Nombre).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Descripcion).HasMaxLength(200);
        builder.HasIndex(r => r.Codigo).IsUnique();
        builder.HasIndex(r => r.Nombre).IsUnique();
    }
}
