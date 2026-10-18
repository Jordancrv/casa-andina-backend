using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public sealed class PersonaConfiguration : IEntityTypeConfiguration<Persona>
{
    public void Configure(EntityTypeBuilder<Persona> builder)
    {
        builder.ToTable("Persona");
        builder.Property(x => x.Id).HasColumnName("PersonaId");
        builder.Property(x => x.TipoPersona).HasColumnType("nchar(1)").IsRequired();
        builder.Property(x => x.Estado).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.Usuario).WithOne(x => x.Persona)
            .HasForeignKey<Usuario>(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PersonaNaturalConfiguration : IEntityTypeConfiguration<PersonaNatural>
{
    public void Configure(EntityTypeBuilder<PersonaNatural> builder)
    {
        builder.ToTable("PersonaNatural"); builder.HasKey(x => x.PersonaId);
        builder.Property(x => x.Nombres).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Apellidos).HasMaxLength(100);
        builder.Property(x => x.FechaNacimiento).HasColumnType("date");
        builder.Property(x => x.Nacionalidad).HasMaxLength(60);
        builder.Property(x => x.Genero).HasColumnType("nchar(1)");
        builder.HasOne(x => x.Persona).WithOne(x => x.Natural).HasForeignKey<PersonaNatural>(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PersonaJuridicaConfiguration : IEntityTypeConfiguration<PersonaJuridica>
{
    public void Configure(EntityTypeBuilder<PersonaJuridica> builder)
    {
        builder.ToTable("PersonaJuridica"); builder.HasKey(x => x.PersonaId);
        builder.Property(x => x.RazonSocial).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NombreComercial).HasMaxLength(150);
        builder.HasOne(x => x.Persona).WithOne(x => x.Juridica).HasForeignKey<PersonaJuridica>(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TipoDocumentoConfiguration : IEntityTypeConfiguration<TipoDocumento>
{
    public void Configure(EntityTypeBuilder<TipoDocumento> builder)
    {
        builder.ToTable("TipoDocumento"); builder.Property(x => x.Id).HasColumnName("TipoDocumentoId");
        builder.Property(x => x.Codigo).HasMaxLength(20).IsRequired(); builder.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        builder.Property(x => x.TipoPersonaPermitida).HasColumnType("nchar(1)").IsRequired(); builder.HasIndex(x => x.Codigo).IsUnique();
    }
}

public sealed class PersonaDocumentoConfiguration : IEntityTypeConfiguration<PersonaDocumento>
{
    public void Configure(EntityTypeBuilder<PersonaDocumento> builder)
    {
        builder.ToTable("PersonaDocumento"); builder.Property(x => x.Id).HasColumnName("PersonaDocumentoId");
        builder.Property(x => x.Numero).HasMaxLength(20).IsRequired(); builder.Property(x => x.PaisEmision).HasMaxLength(60); builder.Property(x => x.FechaVencimiento).HasColumnType("date");
        builder.HasIndex(x => new { x.TipoDocumentoId, x.Numero }).IsUnique();
        builder.HasOne(x => x.Persona).WithMany(x => x.Documentos).HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TipoDocumento).WithMany(x => x.Documentos).HasForeignKey(x => x.TipoDocumentoId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PersonaContactoConfiguration : IEntityTypeConfiguration<PersonaContacto>
{
    public void Configure(EntityTypeBuilder<PersonaContacto> builder)
    {
        builder.ToTable("PersonaContacto");
        builder.Property(x => x.Id).HasColumnName("PersonaContactoId");
        builder.Property(x => x.TipoContacto).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Valor).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.Valor).IsUnique().HasFilter("[TipoContacto] = N'Correo'")
            .HasDatabaseName("UX_PersonaContacto_Correo");
        builder.HasIndex(x => new { x.PersonaId, x.TipoContacto }).IsUnique()
            .HasFilter("[EsPrincipal] = 1").HasDatabaseName("UX_PersonaContacto_Principal");
        builder.HasOne(x => x.Persona).WithMany(x => x.Contactos)
            .HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class HuespedConfiguration : IEntityTypeConfiguration<Huesped>
{
    public void Configure(EntityTypeBuilder<Huesped> builder)
    {
        builder.ToTable("Huesped");
        builder.Property(x => x.Id).HasColumnName("HuespedId");
        builder.HasIndex(x => x.PersonaId).IsUnique();
        builder.HasOne(x => x.PersonaNatural).WithOne(x => x.Huesped)
            .HasForeignKey<Huesped>(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AsignacionHuespedConfiguration : IEntityTypeConfiguration<AsignacionHuesped>
{
    public void Configure(EntityTypeBuilder<AsignacionHuesped> builder)
    {
        builder.ToTable("AsignacionHuesped");
        builder.HasKey(x => new { x.ReservaHabitacionId, x.HuespedId });
        builder.Property(x => x.Observacion).HasMaxLength(250);
        builder.HasOne(x => x.ReservaHabitacion).WithMany(x => x.Huespedes)
            .HasForeignKey(x => x.ReservaHabitacionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Huesped).WithMany(x => x.Asignaciones)
            .HasForeignKey(x => x.HuespedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ReservaHabitacionId).IsUnique()
            .HasFilter("[EsTitular] = 1").HasDatabaseName("UX_AsignacionHuesped_Titular");
    }
}

public sealed class AuditoriaConfiguration : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> builder)
    {
        builder.ToTable("Auditoria");
        builder.Property(x => x.Id).HasColumnName("AuditoriaId");
        builder.Property(x => x.FechaUtc).HasColumnType("datetime2");
        builder.Property(x => x.Accion).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Entidad).HasMaxLength(128).IsRequired();
        builder.Property(x => x.EntidadId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Antes).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Despues).HasColumnType("nvarchar(max)");
        builder.Property(x => x.DireccionIp).HasMaxLength(45);
        builder.HasIndex(x => x.FechaUtc).HasDatabaseName("IX_Auditoria_FechaUtc");
        builder.HasIndex(x => new { x.Entidad, x.EntidadId, x.FechaUtc }).HasDatabaseName("IX_Auditoria_Entidad");
        builder.HasIndex(x => new { x.UsuarioId, x.FechaUtc }).HasDatabaseName("IX_Auditoria_Usuario_Fecha");
        builder.HasIndex(x => new { x.SedeId, x.FechaUtc }).HasDatabaseName("IX_Auditoria_Sede_Fecha");
        builder.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Sede).WithMany().HasForeignKey(x => x.SedeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
