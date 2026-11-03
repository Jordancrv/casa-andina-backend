using CasaAndina.Application.Common;
using CasaAndina.Application.Recepcion;
using CasaAndina.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CasaAndina.Infrastructure.Services;

public sealed class EstadiasWorker(IServiceScopeFactory scopes, ILogger<EstadiasWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                // Cerrar primero permite reutilizar habitaciones sin violar la estadía activa única.
                await ProcesarLoteAsync(true, stoppingToken);
                await ProcesarLoteAsync(false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "No se pudo ejecutar el ciclo de estadías; se reintentará."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcesarLoteAsync(bool salida, CancellationToken ct)
    {
        var ultimoId = 0;
        var fecha = PeruBusinessTime.Today;
        while (!ct.IsCancellationRequested)
        {
            using var listing = scopes.CreateScope();
            var db = listing.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var query = db.ReservaHabitaciones.AsNoTracking().Where(x => x.Id > ultimoId
                && x.Reserva.Estado == "Reservado" && x.FechaCheckOutReal == null);
            query = salida ? query.Where(x => x.FechaCheckOut < fecha)
                : query.Where(x => x.FechaCheckInReal == null && x.FechaCheckIn <= fecha && x.FechaCheckOut >= fecha);
            var ids = await query.OrderBy(x => x.Id).Select(x => x.Id).Take(200).ToListAsync(ct);
            if (ids.Count == 0) return;
            foreach (var id in ids)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<AutomatizarEstadiasService>()
                        .ProcesarAsync(id, salida, DateTime.UtcNow, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogError(ex, "Movimiento automático pendiente para detalle {DetalleId}; se reintentará.", id); }
            }
            ultimoId = ids[^1];
        }
    }
}
