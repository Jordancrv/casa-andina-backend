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

        // Estas propiedades heredadas no existen en la tabla Reserva.
        builder.Ignore(r => r.Activo);
        builder.Ignore(r => r.FechaActualizacion);

        builder.HasIndex(r => r.Estado).HasDatabaseName("IX_Reserva_Estado");

        // FK → Cliente
        builder.HasOne(r => r.Cliente)
            .WithMany(c => c.Reservas)
            .HasForeignKey(r => r.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Sede).WithMany()
            .HasForeignKey(r => r.SedeId).OnDelete(DeleteBehavior.Restrict);

        // FK opcional → Usuario (quien creó la reserva en recepción)
        builder.HasOne(r => r.CreadoPorUsuario)
            .WithMany(u => u.Reservas)
            .HasForeignKey(r => r.CreadoPorUsuarioId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReservaHabitacionConfiguration : IEntityTypeConfiguration<ReservaHabitacion>
{
    public void Configure(EntityTypeBuilder<ReservaHabitacion> builder)
    {
        builder.ToTable("ReservaHabitacion");
        builder.Property(x => x.Id).HasColumnName("ReservaHabitacionId");
        builder.Property(x => x.FechaCheckIn).HasColumnType("date");
        builder.Property(x => x.FechaCheckOut).HasColumnType("date");
        builder.Property(x => x.FechaCheckInReal).HasColumnType("datetime2(7)").IsConcurrencyToken();
        builder.Property(x => x.FechaCheckOutReal).HasColumnType("datetime2(7)").IsConcurrencyToken();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.CheckInPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.CheckOutPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.HabitacionId).IsUnique()
            .HasFilter("[FechaCheckInReal] IS NOT NULL AND [FechaCheckOutReal] IS NULL")
            .HasDatabaseName("UX_ReservaHabitacion_EstadiaActiva");
        builder.Property(x => x.CantidadPersonas).HasColumnType("smallint");
        builder.Property(x => x.PrecioAcordado).HasColumnType("decimal(10,2)");
        builder.Property(x => x.Subtotal).HasColumnType("decimal(10,2)");
        builder.HasIndex(x => new { x.HabitacionId, x.FechaCheckIn, x.FechaCheckOut })
            .HasDatabaseName("IX_ReservaHabitacion_Habitacion_Fechas");
        builder.HasIndex(x => new { x.ReservaId, x.HabitacionId, x.FechaCheckIn, x.FechaCheckOut })
            .IsUnique().HasDatabaseName("UQ_ReservaHabitacion_Reserva_Habitacion_Fechas");
        builder.HasOne(x => x.Reserva).WithMany(x => x.Habitaciones)
            .HasForeignKey(x => x.ReservaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Habitacion).WithMany()
            .HasForeignKey(x => x.HabitacionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReservaServicioConfiguration : IEntityTypeConfiguration<ReservaServicio>
{
    public void Configure(EntityTypeBuilder<ReservaServicio> builder)
    {
        builder.ToTable("ReservaServicio");
        builder.HasKey(x => new { x.ReservaId, x.ServicioId });
        builder.Property(x => x.Cantidad).HasColumnType("int");
        builder.Property(x => x.PrecioUnitario).HasColumnType("decimal(10,2)");
        builder.Property(x => x.Subtotal)
            .HasColumnType("decimal(10,2)")
            .HasComputedColumnSql("(CONVERT(decimal(10,2), [Cantidad] * [PrecioUnitario]))", stored: true);
        builder.HasOne(x => x.Reserva).WithMany(x => x.Servicios)
            .HasForeignKey(x => x.ReservaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Servicio).WithMany(x => x.Reservas)
            .HasForeignKey(x => x.ServicioId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ComprobanteConfiguration : IEntityTypeConfiguration<Comprobante>
{
    public void Configure(EntityTypeBuilder<Comprobante> builder)
    {
        builder.ToTable("Comprobante", table =>
            table.HasCheckConstraint(
                "CK_Comprobante_TipoComprobante",
                "[TipoComprobante] IN (N'Boleta', N'Factura')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ComprobanteId");
        builder.Property(x => x.NumeroComprobante).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.NumeroComprobante).IsUnique().HasDatabaseName("UQ_Comprobante_Numero");
        builder.Property(x => x.Tipo)
            .HasColumnName("TipoComprobante")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(x => x.FechaEmision).HasColumnType("datetime2");
        builder.Property(x => x.MontoTotal).HasColumnType("decimal(10,2)");
        builder.Property(x => x.UrlPdf).HasMaxLength(500);
        builder.HasOne(x => x.Reserva).WithOne(x => x.Comprobante)
            .HasForeignKey<Comprobante>(x => x.ReservaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ReservaId).IsUnique().HasDatabaseName("UQ_Comprobante_Reserva");
    }
}

/// <summary>
/// Configuración del perfil comercial de la persona.
/// </summary>
public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Cliente");
        builder.Property(c => c.Id).HasColumnName("ClienteId");

        builder.Property(c => c.Estado).HasMaxLength(20);
        builder.Property(c => c.FechaRegistro);

        builder.HasOne(c => c.Persona)
            .WithOne(p => p.Cliente)
            .HasForeignKey<Cliente>(c => c.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.PersonaId).IsUnique();
    }
}
