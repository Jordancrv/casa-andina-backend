using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public sealed class EstadoPagoConfiguration : IEntityTypeConfiguration<EstadoPago>
{
    public void Configure(EntityTypeBuilder<EstadoPago> builder)
    {
        builder.ToTable("EstadoPago");
        builder.HasKey(x => x.Id).HasName("PK_EstadoPago");
        builder.Property(x => x.Id).HasColumnName("EstadoPagoId");
        builder.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Nombre).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Descripcion).HasMaxLength(200);
        builder.Property(x => x.EsFinal).HasDefaultValue(false);
        builder.Property(x => x.Activo).HasDefaultValue(true);
        builder.HasIndex(x => x.Codigo).IsUnique().HasDatabaseName("UQ_EstadoPago_Codigo");
        builder.HasIndex(x => x.Nombre).IsUnique().HasDatabaseName("UQ_EstadoPago_Nombre");
    }
}

public sealed class MetodoPagoConfiguration : IEntityTypeConfiguration<MetodoPago>
{
    public void Configure(EntityTypeBuilder<MetodoPago> builder)
    {
        builder.ToTable("MetodoPago", table => table.HasCheckConstraint(
            "CK_MetodoPago_Categoria",
            "[Categoria] IN (N'Tarjeta', N'BilleteraDigital', N'Transferencia', N'Efectivo', N'Otro')"));
        builder.HasKey(x => x.Id).HasName("PK_MetodoPago");
        builder.Property(x => x.Id).HasColumnName("MetodoPagoId");
        builder.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Categoria).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Activo).HasDefaultValue(true);
        builder.Property(x => x.FechaCreacion).HasColumnType("datetime2").HasDefaultValueSql("(sysdatetime())");
        builder.HasIndex(x => x.Codigo).IsUnique().HasDatabaseName("UQ_MetodoPago_Codigo");
        builder.HasIndex(x => x.Nombre).IsUnique().HasDatabaseName("UQ_MetodoPago_Nombre");
    }
}

public sealed class PagoConfiguration : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> builder)
    {
        builder.ToTable("Pago", table =>
        {
            table.HasCheckConstraint("CK_Pago_Importe", "[Importe] > (0)");
            table.HasCheckConstraint("CK_Pago_Moneda", "[Moneda] LIKE '[A-Z][A-Z][A-Z]'");
            table.HasCheckConstraint("CK_Pago_ReferenciaProveedor",
                "[ReferenciaProveedor] IS NULL OR LEN(LTRIM(RTRIM([ReferenciaProveedor]))) > (0)");
        });
        builder.HasKey(x => x.Id).HasName("PK_Pago");
        builder.Property(x => x.Id).HasColumnName("PagoId");
        builder.Property(x => x.ReferenciaProveedor).HasMaxLength(100);
        builder.Property(x => x.Importe).HasColumnType("decimal(10,2)");
        builder.Property(x => x.Moneda).HasColumnType("char(3)").HasDefaultValue("PEN");
        builder.Property(x => x.FechaCreacion).HasColumnType("datetime2").HasDefaultValueSql("(sysdatetime())");
        builder.Property(x => x.FechaActualizacion).HasColumnType("datetime2");
        builder.HasOne(x => x.MetodoPago).WithMany(x => x.Pagos)
            .HasForeignKey(x => x.MetodoPagoId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Pago_MetodoPago");
        builder.HasOne(x => x.EstadoPago).WithMany(x => x.Pagos)
            .HasForeignKey(x => x.EstadoPagoId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Pago_EstadoPago");
        builder.HasIndex(x => x.ReferenciaProveedor).IsUnique()
            .HasFilter("[ReferenciaProveedor] IS NOT NULL")
            .HasDatabaseName("UX_Pago_ReferenciaProveedor");
        builder.HasIndex(x => new { x.EstadoPagoId, x.FechaCreacion })
            .HasDatabaseName("IX_Pago_Estado_Fecha");
    }
}

public sealed class DetallePagoConfiguration : IEntityTypeConfiguration<DetallePago>
{
    public void Configure(EntityTypeBuilder<DetallePago> builder)
    {
        builder.ToTable("DetallePago", table => table.HasCheckConstraint(
            "CK_DetallePago_MontoAplicado", "[MontoAplicado] > (0)"));
        builder.HasKey(x => x.Id).HasName("PK_DetallePago");
        builder.Property(x => x.Id).HasColumnName("DetallePagoId");
        builder.Property(x => x.MontoAplicado).HasColumnType("decimal(10,2)");
        builder.Property(x => x.FechaAplicacion).HasColumnType("datetime2").HasDefaultValueSql("(sysdatetime())");
        builder.HasOne(x => x.Reserva).WithMany(x => x.Pagos)
            .HasForeignKey(x => x.ReservaId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DetallePago_Reserva");
        builder.HasOne(x => x.Pago).WithMany(x => x.Detalles)
            .HasForeignKey(x => x.PagoId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DetallePago_Pago");
        builder.HasIndex(x => new { x.ReservaId, x.PagoId }).IsUnique()
            .HasDatabaseName("UQ_DetallePago_Reserva_Pago");
        builder.HasIndex(x => x.PagoId).HasDatabaseName("IX_DetallePago_Pago");
    }
}
