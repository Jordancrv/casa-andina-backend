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
public sealed record GetRecepcionQuery(string Vista = "llegadas", DateTime? Fecha = null, string? Codigo = null, string? Habitacion = null, int Pagina = 1, int TamanoPagina = 20)
    : IRequest<IReadOnlyList<RecepcionHabitacionDto>>;
public sealed record RegistrarMovimientoCommand(int Id, bool EsCheckOut) : IRequest;

public static class RecepcionRules
{
    public static int Sede(ICurrentUserService user)
    {
        if (!user.TienePermiso("recepcion.ver")
            || user.SedeId is not int sede || user.UsuarioId is null)
            throw new ForbiddenAccessException("Recepción requiere un administrador o recepcionista con sede asignada.");
        return sede;
    }

    public static void ValidarMovimiento(ReservaHabitacion detail, bool salida, DateTime hoy)
    {
        if (detail.Reserva.Estado != "Reservado")
            throw new ConflictException("Solo se pueden operar reservas en estado Reservado.");
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
        if (request.Vista is not ("llegadas" or "estadia" or "salidas" or "historial"))
            throw new ConflictException("Seleccione una vista válida de recepción.");
        if (request.Pagina < 1 || request.TamanoPagina is < 1 or > 100 || (long)(request.Pagina - 1) * request.TamanoPagina > int.MaxValue)
            throw new ConflictException("Seleccione una página y tamaño válidos.");
        if (request.Codigo?.Length > 12) throw new ConflictException("El código de reserva admite hasta 12 caracteres.");
        var fecha = (request.Fecha ?? PeruBusinessTime.Today).Date;
        var query = context.ReservaHabitaciones.AsNoTracking()
            .Where(x => x.Reserva.SedeId == sede);
        if (request.Habitacion?.Length > 20) throw new ConflictException("El número de habitación admite hasta 20 caracteres.");
        if (!string.IsNullOrWhiteSpace(request.Habitacion)) query = query.Where(x => x.Habitacion.Numero == request.Habitacion.Trim());
        if (request.Vista != "historial")
            query = query.Where(x => x.Reserva.Estado == "Reservado");
        if (!string.IsNullOrWhiteSpace(request.Codigo))
            query = query.Where(x => x.Reserva.CodigoReserva == request.Codigo.Trim().ToUpper());
        else
            query = request.Vista switch
            {
                "historial" => query,
                "estadia" => query.Where(x => x.FechaCheckInReal != null && x.FechaCheckOutReal == null),
                "salidas" => query.Where(x => x.FechaCheckInReal != null && x.FechaCheckOutReal == null && x.FechaCheckOut <= fecha),
                _ => query.Where(x => x.FechaCheckInReal == null && x.FechaCheckIn <= fecha && x.FechaCheckOut > fecha)
            };
        var ordered = (request.Vista == "historial" ? query.OrderByDescending(x => x.FechaCheckIn) : query.OrderBy(x => x.FechaCheckIn)).ThenBy(x => x.Id);
        var page = request.Vista == "historial"
            ? ordered.Skip((request.Pagina - 1) * request.TamanoPagina).Take(request.TamanoPagina + 1)
            : ordered.Take(100);
        return await page
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
