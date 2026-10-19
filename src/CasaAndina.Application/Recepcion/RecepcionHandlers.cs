using System.Data;
using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Recepcion;

public sealed record RecepcionHuespedDto(string Nombre, bool EsTitular);
public sealed record RecepcionHabitacionDto(int Id, string CodigoReserva, string Numero, string Tipo,
    DateTime EntradaPrevista, DateTime SalidaPrevista, DateTime? EntradaReal, DateTime? SalidaReal,
    string EstadoReserva, string EstadoHabitacion, IReadOnlyList<RecepcionHuespedDto> Huespedes);
public sealed record GetRecepcionQuery(string Vista = "llegadas", DateTime? Fecha = null, string? Codigo = null)
    : IRequest<IReadOnlyList<RecepcionHabitacionDto>>;
public sealed record RegistrarMovimientoCommand(int Id, bool EsCheckOut) : IRequest;

public static class RecepcionRules
{
    public static int Sede(ICurrentUserService user)
    {
        if (user.Rol is not (RolUsuario.Administrador or RolUsuario.Recepcion)
            || user.SedeId is not int sede || user.UsuarioId is null)
            throw new ForbiddenAccessException("Recepción requiere un administrador o recepcionista con sede asignada.");
        return sede;
    }

    public static void ValidarMovimiento(ReservaHabitacion detail, bool salida, DateTime hoy)
    {
        if (detail.Reserva.Estado != "Confirmada")
            throw new ConflictException("Solo se pueden operar reservas confirmadas.");
        if (salida)
        {
            if (detail.FechaCheckInReal is null) throw new ConflictException("Primero debe registrar el check-in.");
            if (detail.FechaCheckOutReal is not null) throw new ConflictException("El check-out ya está registrado.");
        }
        else
        {
            if (detail.FechaCheckInReal is not null) throw new ConflictException("El check-in ya está registrado.");
            if (hoy.Date < detail.FechaCheckIn.Date || hoy.Date >= detail.FechaCheckOut.Date)
                throw new ConflictException("El check-in debe realizarse dentro de las fechas contratadas.");
            if (!detail.Habitacion.Activo || detail.Habitacion.Estado != "Disponible")
                throw new ConflictException("La habitación debe estar activa y disponible para registrar el ingreso.");
        }
    }
}

public sealed class GetRecepcionQueryHandler(IApplicationDbContext context, ICurrentUserService user)
    : IRequestHandler<GetRecepcionQuery, IReadOnlyList<RecepcionHabitacionDto>>
{
    public async Task<IReadOnlyList<RecepcionHabitacionDto>> Handle(GetRecepcionQuery request, CancellationToken ct)
    {
        var sede = RecepcionRules.Sede(user);
        if (request.Vista is not ("llegadas" or "estadia" or "salidas"))
            throw new ConflictException("Seleccione una vista válida de recepción.");
        if (request.Codigo?.Length > 12) throw new ConflictException("El código de reserva admite hasta 12 caracteres.");
        var fecha = (request.Fecha ?? PeruBusinessTime.Today).Date;
        var query = context.ReservaHabitaciones.AsNoTracking()
            .Where(x => x.Reserva.SedeId == sede && x.Reserva.Estado == "Confirmada");
        if (!string.IsNullOrWhiteSpace(request.Codigo))
            query = query.Where(x => x.Reserva.CodigoReserva == request.Codigo.Trim().ToUpper());
        else
            query = request.Vista switch
            {
                "estadia" => query.Where(x => x.FechaCheckInReal != null && x.FechaCheckOutReal == null),
                "salidas" => query.Where(x => x.FechaCheckInReal != null && x.FechaCheckOutReal == null && x.FechaCheckOut <= fecha),
                _ => query.Where(x => x.FechaCheckInReal == null && x.FechaCheckIn <= fecha && x.FechaCheckOut > fecha)
            };
        return await query.OrderBy(x => x.FechaCheckIn).ThenBy(x => x.Id).Take(100)
            .Select(x => new RecepcionHabitacionDto(x.Id, x.Reserva.CodigoReserva, x.Habitacion.Numero,
                x.Habitacion.TipoHabitacion.Nombre, x.FechaCheckIn, x.FechaCheckOut, x.FechaCheckInReal, x.FechaCheckOutReal,
                x.Reserva.Estado, x.Habitacion.Estado, x.Huespedes.OrderByDescending(g => g.EsTitular)
                    .Select(g => new RecepcionHuespedDto(g.Huesped.PersonaNatural.Nombres + " " + g.Huesped.PersonaNatural.Apellidos, g.EsTitular)).ToArray()))
            .ToListAsync(ct);
    }
}

public sealed class RegistrarMovimientoCommandHandler(IApplicationDbContext context, ICurrentUserService user)
    : IRequestHandler<RegistrarMovimientoCommand>
{
    public async Task Handle(RegistrarMovimientoCommand request, CancellationToken ct)
    {
        var sede = RecepcionRules.Sede(user);
        await using var transaction = await context.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var detail = await context.ReservaHabitaciones.Include(x => x.Habitacion)
            .Include(x => x.Reserva).ThenInclude(x => x.Habitaciones)
            .SingleOrDefaultAsync(x => x.Id == request.Id && x.Reserva.SedeId == sede, ct)
            ?? throw new NotFoundException("Habitación reservada", request.Id);
        RecepcionRules.ValidarMovimiento(detail, request.EsCheckOut, PeruBusinessTime.Today);
        var now = DateTime.UtcNow;
        if (request.EsCheckOut)
        {
            if (now < detail.FechaCheckInReal) throw new ConflictException("La salida no puede ser anterior al ingreso.");
            detail.FechaCheckOutReal = now;
            detail.CheckOutPorUsuarioId = user.UsuarioId;
            // No se ofrece como lista hasta que el personal confirme la preparación.
            detail.Habitacion.Estado = "Bloqueada";
            if (detail.Reserva.Habitaciones.All(x => x.FechaCheckOutReal != null)) detail.Reserva.Estado = "Completada";
        }
        else
        {
            var occupied = await context.ReservaHabitaciones.AnyAsync(x => x.HabitacionId == detail.HabitacionId
                && x.Id != detail.Id && x.FechaCheckInReal != null && x.FechaCheckOutReal == null, ct);
            if (occupied) throw new ConflictException("La habitación aún tiene una estadía sin check-out.");
            detail.FechaCheckInReal = now;
            detail.CheckInPorUsuarioId = user.UsuarioId;
            detail.Habitacion.Estado = "Ocupada";
        }
        context.Auditoria.Add(new Auditoria
        {
            FechaUtc = now, UsuarioId = user.UsuarioId, SedeId = sede, Accion = "UPDATE",
            Entidad = "ReservaHabitacion", EntidadId = detail.Id.ToString(),
            Despues = System.Text.Json.JsonSerializer.Serialize(new { Movimiento = request.EsCheckOut ? "CHECK_OUT" : "CHECK_IN", FechaUtc = now })
        });
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("La estadía cambió. Actualice la lista antes de continuar."); }
        await transaction.CommitAsync(ct);
    }
}
