using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasaAndina.Infrastructure.Services;

public sealed class CalendarSyncWorker(IServiceScopeFactory scopes, IOptions<GoogleCalendarOptions> options,
    ILogger<CalendarSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!options.Value.Configurado) continue;
            try
            {
                using var listing = scopes.CreateScope();
                var db = listing.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var clients = await db.Set<ConexionCalendario>().AsNoTracking()
                    .Where(x => x.RefreshTokenProtegido != null && !x.RequiereReconectar)
                    .Where(x => db.Usuarios.Any(u => u.Activo && u.Persona.Cliente != null
                        && u.Persona.Cliente.Id == x.ClienteId && u.Roles.Any(r => r.Rol.Codigo == "CLIENTE")))
                    .Select(x => x.ClienteId).ToListAsync(stoppingToken);
                foreach (var client in clients)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<ICalendarioService>().SincronizarAsync(client, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception) { logger.LogWarning("Sincronización de Calendar pendiente; se reintentará en el próximo ciclo."); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("Calendar no pudo iniciar su ciclo. Revise la conexión y la migración de base de datos."); }
        }
    }
}
