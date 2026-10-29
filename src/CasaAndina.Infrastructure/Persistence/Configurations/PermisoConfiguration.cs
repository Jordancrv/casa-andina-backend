using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CasaAndina.Infrastructure.Persistence.Configurations;
public sealed class PermisoConfiguration : IEntityTypeConfiguration<Permiso> {
 public void Configure(EntityTypeBuilder<Permiso> b) { b.ToTable("Permiso"); b.HasKey(p=>p.Codigo); b.Property(p=>p.Codigo).HasMaxLength(80); b.Property(p=>p.Seccion).HasMaxLength(40); }
}
public sealed class RolPermisoConfiguration : IEntityTypeConfiguration<RolPermiso> {
 public void Configure(EntityTypeBuilder<RolPermiso> b) { b.ToTable("RolPermiso"); b.HasKey(p=>new {p.RolId,p.PermisoCodigo}); b.Property(p=>p.PermisoCodigo).HasMaxLength(80); b.HasOne(p=>p.Rol).WithMany(r=>r.Permisos).HasForeignKey(p=>p.RolId).OnDelete(DeleteBehavior.Cascade); b.HasOne(p=>p.Permiso).WithMany().HasForeignKey(p=>p.PermisoCodigo).OnDelete(DeleteBehavior.Restrict); }
}