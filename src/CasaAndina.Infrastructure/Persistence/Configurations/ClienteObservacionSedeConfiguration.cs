using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CasaAndina.Infrastructure.Persistence.Configurations;

public sealed class ClienteObservacionSedeConfiguration : IEntityTypeConfiguration<ClienteObservacionSede>
{
    public void Configure(EntityTypeBuilder<ClienteObservacionSede> builder)
    {
        builder.ToTable("ClienteObservacionSede");
        builder.HasKey(x => new { x.ClienteId, x.SedeId });
        builder.Property(x => x.Observacion).HasMaxLength(2000).IsRequired();
        builder.HasOne<Cliente>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Sede>().WithMany().HasForeignKey(x => x.SedeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.ActualizadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
