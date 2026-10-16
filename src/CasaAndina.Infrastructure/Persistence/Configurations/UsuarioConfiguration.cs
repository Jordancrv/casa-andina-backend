using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public sealed class UsuarioRolConfiguration : IEntityTypeConfiguration<UsuarioRol>
{
    public void Configure(EntityTypeBuilder<UsuarioRol> builder)
    {
        builder.ToTable("UsuarioRol");
        builder.HasKey(x => new { x.UsuarioId, x.RolId });
        builder.Property(x => x.FechaAsignacion).HasColumnType("datetime2");
        builder.HasOne(x => x.Usuario).WithMany(x => x.Roles)
            .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Rol).WithMany(x => x.Usuarios)
            .HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuario");

        // PK: UsuarioId en BD → Id en dominio
        builder.Property(u => u.Id).HasColumnName("UsuarioId");

        builder.Property(u => u.PasswordHash)
            .HasColumnName("ContrasenaHash")
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(u => u.Correo).HasMaxLength(150).IsRequired();
        builder.HasIndex(u => u.Correo).IsUnique().HasDatabaseName("UX_Usuario_Correo");
        builder.Property(u => u.UltimoAcceso);

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

        builder.Property(u => u.PersonaId).HasColumnName("PersonaId");
        builder.HasIndex(u => u.PersonaId).IsUnique().HasDatabaseName("UX_Usuario_Persona");

        builder.HasOne(u => u.Sede)
            .WithMany()
            .HasForeignKey(u => u.SedeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // 'FechaActualizacion' de BaseEntity no existe en la tabla Usuario
        builder.Ignore(u => u.FechaActualizacion);
    }
}
