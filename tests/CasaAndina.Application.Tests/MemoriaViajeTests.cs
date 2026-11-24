using CasaAndina.Api.Controllers;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaAndina.Application.Tests;

public class MemoriaViajeTests
{
    private sealed record ClienteActual(int Id) : ICurrentUserService
    {
        public int? ClienteId => Id;
        public int? UsuarioId => Id;
        public int? SedeId => null;
        public RolUsuario? Rol => RolUsuario.Cliente;
    }

    [Fact]
    public async Task Preferencias_y_valoraciones_son_independientes_por_cliente()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Clientes.AddRange(new Cliente { Id = 1 }, new Cliente { Id = 2 });
        await db.SaveChangesAsync();
        var api = new ChatbotController(null!);
        var uno = new ClienteActual(1);
        var dos = new ClienteActual(2);
        await api.GuardarPreferencias(new(true, false), db, uno, default);
        Assert.True((await db.Clientes.FindAsync(1))!.RecordarPlaya);
        Assert.False((await db.Clientes.FindAsync(2))!.RecordarPlaya);
        await api.GuardarPreferencias(new(false, false), db, uno, default);
        Assert.False((await db.Clientes.FindAsync(1))!.RecordarPlaya);
        var voto = new ChatbotController.Valoracion(true, null, "Piura", DateTimeOffset.UtcNow);
        await api.Valorar(voto, db, uno, default);
        await api.Valorar(voto with { Util = false, Motivo = "Prefiero playa" }, db, uno, default);
        await api.Valorar(voto, db, dos, default);
        Assert.Equal(2, await db.ValoracionesViaje.CountAsync());
        Assert.Equal("Prefiero playa", (await db.ValoracionesViaje.SingleAsync(v => v.ClienteId == 1)).Motivo);
    }
}
