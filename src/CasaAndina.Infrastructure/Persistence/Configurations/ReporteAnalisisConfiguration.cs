using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CasaAndina.Infrastructure.Persistence.Configurations;
public sealed class ReporteAnalisisConfiguration : IEntityTypeConfiguration<ReporteAnalisis>
{
    public void Configure(EntityTypeBuilder<ReporteAnalisis> builder)
    {
        builder.ToTable("ReporteAnalisis");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("ReporteAnalisisId");
        builder.Property(x => x.Periodo).HasMaxLength(10).IsRequired();
        builder.Property(x => x.ContenidoJson).IsRequired();
        builder.HasIndex(x => new { x.SedeId, x.Periodo, x.GeneradoUtc });
        builder.HasOne<Sede>().WithMany().HasForeignKey(x => x.SedeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
