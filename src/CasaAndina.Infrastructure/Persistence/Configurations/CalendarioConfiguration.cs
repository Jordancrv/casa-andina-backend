using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public sealed class ConexionCalendarioConfiguration : IEntityTypeConfiguration<ConexionCalendario>
{
    public void Configure(EntityTypeBuilder<ConexionCalendario> b)
    {
        b.ToTable("ConexionCalendario");
        b.HasKey(x => x.ClienteId);
        b.Property(x => x.ClienteId).ValueGeneratedNever();
        b.Property(x => x.CalendarioId).HasMaxLength(1024);
        b.Property(x => x.RefreshTokenProtegido).HasMaxLength(4000);
        b.HasOne<Cliente>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EventoCalendarioConfiguration : IEntityTypeConfiguration<EventoCalendario>
{
    public void Configure(EntityTypeBuilder<EventoCalendario> b)
    {
        b.ToTable("EventoCalendario");
        b.HasKey(x => new { x.ClienteId, x.ReservaId });
        b.Property(x => x.EventoId).HasMaxLength(64);
        b.Property(x => x.Huella).HasMaxLength(64);
        b.HasOne<ConexionCalendario>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Reserva>().WithMany().HasForeignKey(x => x.ReservaId).OnDelete(DeleteBehavior.Restrict);
    }
}
