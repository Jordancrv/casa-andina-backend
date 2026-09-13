using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuario");

        // PK: UsuarioId en BD → Id en dominio
        builder.Property(u => u.Id).HasColumnName("UsuarioId");

        // Correo → Email
        builder.Property(u => u.Email)
            .HasColumnName("Correo")
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();

        // ContrasenaHash → PasswordHash
        builder.Property(u => u.PasswordHash)
            .HasColumnName("ContrasenaHash")
            .HasMaxLength(256)
            .IsRequired();

        // Nombre → NombreCompleto
        builder.Property(u => u.NombreCompleto)
            .HasColumnName("Nombre")
            .HasMaxLength(150)
            .IsRequired();

        // FechaRegistro → FechaCreacion (BaseEntity)
        builder.Property(u => u.FechaCreacion)
            .HasColumnName("FechaRegistro");

        // Estado ('Activo'/'Inactivo') → Activo (bool)
        builder.Property(u => u.Activo)
            .HasColumnName("Estado")
            .HasConversion(
                v => v ? "Activo" : "Inactivo",
                v => v == "Activo")
            .HasMaxLength(20);

        // RolId: FK a tabla Rol
        builder.Property(u => u.RolId).HasColumnName("RolId");

        // 'Rol' es una propiedad calculada (no stored) → EF no debe mapearla a columna
        builder.Ignore(u => u.Rol);

        // 'FechaActualizacion' de BaseEntity no existe en la tabla Usuario
        builder.Ignore(u => u.FechaActualizacion);
    }
}
