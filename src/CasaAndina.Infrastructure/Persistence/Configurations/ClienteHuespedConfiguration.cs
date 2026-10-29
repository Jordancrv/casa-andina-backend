using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CasaAndina.Infrastructure.Persistence.Configurations;
public sealed class ClienteHuespedConfiguration : IEntityTypeConfiguration<ClienteHuesped>
{
 public void Configure(EntityTypeBuilder<ClienteHuesped> b)
 {
  b.ToTable("ClienteHuesped"); b.Property(x => x.Id).HasColumnName("ClienteHuespedId");
  b.Property(x => x.Nombres).HasMaxLength(200); b.Property(x => x.Apellidos).HasMaxLength(100); b.Property(x => x.Dni).HasMaxLength(8); b.Property(x => x.Sexo).HasMaxLength(1); b.Property(x => x.Correo).HasMaxLength(150); b.Property(x => x.Telefono).HasMaxLength(16); b.Property(x => x.FechaNacimiento).HasColumnType("date");
  b.HasIndex(x => new { x.ClienteId, x.HuespedId }).IsUnique(); b.HasIndex(x => new { x.ClienteId, x.Dni }).IsUnique();
  b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Huesped).WithMany().HasForeignKey(x => x.HuespedId).OnDelete(DeleteBehavior.Restrict);
 }
}
