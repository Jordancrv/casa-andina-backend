using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public class ReservaConfiguration : IEntityTypeConfiguration<Reserva>
{
    public void Configure(EntityTypeBuilder<Reserva> builder)
    {
        builder.ToTable("Reserva");
        builder.Property(r => r.Id).HasColumnName("ReservaId");

        // CodigoReserva en BD → CodigoReserva en dominio
        builder.Property(r => r.CodigoReserva).HasMaxLength(12).IsRequired();
        builder.HasIndex(r => r.CodigoReserva).IsUnique();

        // PrecioTotal en BD → PrecioTotal en dominio
        builder.Property(r => r.PrecioTotal).HasColumnType("decimal(10,2)");

        builder.Property(r => r.Estado).HasMaxLength(20);
        builder.Property(r => r.Canal).HasMaxLength(20);

        builder.Property(r => r.NumAdultos).HasColumnType("tinyint");
        builder.Property(r => r.NumNinos).HasColumnType("tinyint");

        // BaseEntity.FechaActualizacion no existe en Reserva
        builder.Ignore(r => r.FechaActualizacion);

        // FK → Cliente
        builder.HasOne(r => r.Cliente)
            .WithMany(c => c.Reservas)
            .HasForeignKey(r => r.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK → Habitacion (relación directa 1:1 en la BD)
        builder.HasOne(r => r.Habitacion)
            .WithMany()
            .HasForeignKey(r => r.HabitacionId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK opcional → Usuario (quien creó la reserva en recepción)
        builder.HasOne(r => r.CreadoPorUsuario)
            .WithMany(u => u.Reservas)
            .HasForeignKey(r => r.CreadoPorUsuarioId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>
/// Configuración de la tabla Cliente.
/// BD: ClienteId, NombreCompleto, DNI, Correo, Telefono,
///     ContrasenaHash, FotoUrl, Estado, FechaRegistro
/// </summary>
public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Cliente");
        builder.Property(c => c.Id).HasColumnName("ClienteId");

        builder.Property(c => c.NombreCompleto).HasMaxLength(200).IsRequired();
        builder.Property(c => c.DNI).HasMaxLength(15).IsRequired();
        builder.Property(c => c.Correo).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Telefono).HasMaxLength(20);
        builder.Property(c => c.ContrasenaHash).HasMaxLength(256);
        builder.Property(c => c.FotoUrl).HasMaxLength(500);
        builder.Property(c => c.Estado).HasMaxLength(20);
        builder.Property(c => c.FechaRegistro);

        builder.HasIndex(c => c.DNI).IsUnique();
        builder.HasIndex(c => c.Correo).IsUnique();
    }
}
