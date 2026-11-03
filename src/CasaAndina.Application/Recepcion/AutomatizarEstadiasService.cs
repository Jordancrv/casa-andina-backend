using System.Data;
using System.Text.Json;
using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Recepcion;

/// <summary>Transiciones automáticas, ejecutadas con un contexto y transacción nuevos por habitación.</summary>
public sealed class AutomatizarEstadiasService(IApplicationDbContext db)
{
    public static DateTime EntradaUtc(DateTime fecha) => PeruBusinessTime.UtcStartOfDay(fecha).AddHours(10);
    public static DateTime SalidaUtc(DateTime fecha) => PeruBusinessTime.UtcStartOfDay(fecha.AddDays(1));

    public async Task<bool> ProcesarAsync(int id, bool salida, DateTime ahoraUtc, CancellationToken ct)
    {
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var detalle = await db.ReservaHabitaciones.Include(x => x.Habitacion)
            .Include(x => x.Reserva).ThenInclude(x => x.Habitaciones)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (detalle is null || detalle.Reserva.Estado != "Reservado" || detalle.FechaCheckOutReal != null)
            return false;
        var entrada = EntradaUtc(detalle.FechaCheckIn);
        var fin = SalidaUtc(detalle.FechaCheckOut);
        if (salida)
        {
            if (ahoraUtc < fin || detalle.FechaCheckInReal > fin) return false;
            // Recuperación tras una caída: registrar los horarios programados, sin inventar un actor humano.
            if (detalle.FechaCheckInReal is null)
            {
                detalle.FechaCheckInReal = entrada;
                detalle.CheckInPorUsuarioId = null;
                Auditar(detalle, "CHECK_IN", entrada, ahoraUtc, recuperado: true);
            }
            detalle.FechaCheckOutReal = fin;
            detalle.CheckOutPorUsuarioId = null;
            var otraAbierta = await db.ReservaHabitaciones.AnyAsync(x => x.HabitacionId == detalle.HabitacionId
                && x.Id != id && x.FechaCheckInReal != null && x.FechaCheckOutReal == null, ct);
            // Respetar bloqueos, mantenimiento y la ocupación por otra estadía.
            if (!otraAbierta && detalle.Habitacion.Activo && detalle.Habitacion.Estado == "Ocupada")
                detalle.Habitacion.Estado = "Disponible";
            if (detalle.Reserva.Habitaciones.All(x => x.FechaCheckOutReal != null))
                detalle.Reserva.Estado = "Completada";
            Auditar(detalle, "CHECK_OUT", fin, ahoraUtc, recuperado: false);
        }
        else
        {
            if (ahoraUtc < entrada || ahoraUtc >= fin || detalle.FechaCheckInReal != null ||
                !detalle.Habitacion.Activo || detalle.Habitacion.Estado != "Disponible") return false;
            if (await db.ReservaHabitaciones.AnyAsync(x => x.HabitacionId == detalle.HabitacionId && x.Id != id
                && x.FechaCheckInReal != null && x.FechaCheckOutReal == null, ct)) return false;
            // Registrar el instante de ejecución: no solapar el ingreso con una salida manual tardía.
            detalle.FechaCheckInReal = ahoraUtc;
            detalle.CheckInPorUsuarioId = null;
            detalle.Habitacion.Estado = "Ocupada";
            Auditar(detalle, "CHECK_IN", ahoraUtc, ahoraUtc, recuperado: false);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private void Auditar(ReservaHabitacion detalle, string movimiento, DateTime fecha, DateTime procesado, bool recuperado)
        => db.Auditoria.Add(new Auditoria {
            FechaUtc = procesado, SedeId = detalle.Reserva.SedeId, UsuarioId = null,
            Accion = "UPDATE", Entidad = "ReservaHabitacion", EntidadId = detalle.Id.ToString(),
            Despues = JsonSerializer.Serialize(new { Movimiento = movimiento, Origen = "AUTOMATICO", FechaUtc = fecha, Recuperado = recuperado })
        });
}
