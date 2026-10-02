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
    }
}

public sealed class PersonaNaturalConfiguration : IEntityTypeConfiguration<PersonaNatural>
{
    public void Configure(EntityTypeBuilder<PersonaNatural> builder)
    {
        builder.ToTable("PersonaNatural"); builder.HasKey(x => x.PersonaId);
        builder.Property(x => x.Nombres).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Apellidos).HasMaxLength(100);
        builder.Property(x => x.FechaNacimiento).HasColumnType("date");
        builder.Property(x => x.Nacionalidad).HasMaxLength(60);
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

public sealed class PersonaCorreoConfiguration : IEntityTypeConfiguration<PersonaCorreo>
{
    public void Configure(EntityTypeBuilder<PersonaCorreo> builder)
    {
        builder.ToTable("PersonaCorreo"); builder.Property(x => x.Id).HasColumnName("PersonaCorreoId"); builder.Property(x => x.Correo).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.Correo).IsUnique(); builder.HasOne(x => x.Persona).WithMany(x => x.Correos).HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PersonaTelefonoConfiguration : IEntityTypeConfiguration<PersonaTelefono>
{
    public void Configure(EntityTypeBuilder<PersonaTelefono> builder)
    {
        builder.ToTable("PersonaTelefono"); builder.Property(x => x.Id).HasColumnName("PersonaTelefonoId"); builder.Property(x => x.Numero).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.PersonaId, x.Numero }).IsUnique(); builder.HasOne(x => x.Persona).WithMany(x => x.Telefonos).HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReservaHuespedConfiguration : IEntityTypeConfiguration<ReservaHuesped>
{
    public void Configure(EntityTypeBuilder<ReservaHuesped> builder)
    {
        builder.ToTable("ReservaHuesped"); builder.HasKey(x => new { x.ReservaId, x.PersonaId }); builder.Property(x => x.Observacion).HasMaxLength(250);
        builder.HasOne(x => x.Reserva).WithMany(x => x.Huespedes).HasForeignKey(x => x.ReservaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Huesped).WithMany(x => x.Reservas).HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
    }
}
