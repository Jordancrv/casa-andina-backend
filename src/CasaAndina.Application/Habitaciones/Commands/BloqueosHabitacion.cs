using System.Data;
using System.Text.Json;
using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Habitaciones.Commands;

public sealed record CrearBloqueoCommand(int HabitacionId, DateTime Inicio, DateTime Fin, string Motivo) : IRequest<int>;
public sealed record CancelarBloqueoCommand(int Id) : IRequest;

public sealed class CrearBloqueoValidator : AbstractValidator<CrearBloqueoCommand>
{
    public CrearBloqueoValidator()
    {
        RuleFor(x => x.HabitacionId).GreaterThan(0);
        RuleFor(x => x.Inicio).Must(x => x.TimeOfDay == TimeSpan.Zero && x.Date >= PeruBusinessTime.Today).WithMessage("La fecha de inicio debe ser hoy o posterior.");
        RuleFor(x => x.Fin).Must((x, fin) => fin.TimeOfDay == TimeSpan.Zero && fin.Date >= x.Inicio.Date && fin.Date < DateTime.MaxValue.Date && (fin.Date - x.Inicio.Date).TotalDays < 366).WithMessage("El fin debe ser igual o posterior al inicio y el periodo no debe exceder 366 días.");
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(200).Must(x => !string.IsNullOrWhiteSpace(x));
    }
}

public sealed class CrearBloqueoHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<CrearBloqueoCommand, int>
{
    public async Task<int> Handle(CrearBloqueoCommand request, CancellationToken ct)
    {
        if (user.SedeId is not int sede || user.UsuarioId is not int usuario || !user.TienePermiso("habitaciones.editar")) throw new ForbiddenAccessException("No tiene permisos para bloquear habitaciones.");
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var room = await db.Habitaciones.SingleOrDefaultAsync(x => x.Id == request.HabitacionId && x.SedeId == sede && x.Activo, ct) ?? throw new NotFoundException("Habitación", request.HabitacionId);
        var fin = request.Fin.Date.AddDays(1);
        if (await db.ReservaHabitaciones.AnyAsync(x => x.HabitacionId == room.Id && x.Reserva.Estado != "Cancelada" && x.FechaCheckIn < fin && x.FechaCheckOut > request.Inicio.Date, ct)) throw new ConflictException("La habitación tiene una reserva durante el periodo indicado.");
        if (await db.BloqueosHabitacion.AnyAsync(x => x.HabitacionId == room.Id && x.Activo && x.Inicio < fin && x.FinExclusivo > request.Inicio.Date, ct)) throw new ConflictException("La habitación ya tiene un bloqueo durante ese periodo.");
        var bloque = new BloqueoHabitacion { HabitacionId = room.Id, Inicio = request.Inicio.Date, FinExclusivo = fin, Motivo = request.Motivo.Trim(), CreadoPorUsuarioId = usuario, CreadoUtc = DateTime.UtcNow, Activo = true };
        db.BloqueosHabitacion.Add(bloque); await db.SaveChangesAsync(ct);
        db.Auditoria.Add(new Auditoria { FechaUtc = DateTime.UtcNow, UsuarioId = usuario, SedeId = sede, Accion = "INSERT", Entidad = "BloqueoHabitacion", EntidadId = bloque.Id.ToString(), Despues = JsonSerializer.Serialize(new { bloque.HabitacionId, bloque.Inicio, bloque.FinExclusivo, bloque.Motivo }) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return bloque.Id;
    }
}

public sealed class CancelarBloqueoHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<CancelarBloqueoCommand>
{
    public async Task Handle(CancelarBloqueoCommand request, CancellationToken ct)
    {
        if (user.SedeId is not int sede || user.UsuarioId is not int usuario || !user.TienePermiso("habitaciones.editar")) throw new ForbiddenAccessException("No tiene permisos para cancelar bloqueos.");
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var b = await db.BloqueosHabitacion.Include(x => x.Habitacion).SingleOrDefaultAsync(x => x.Id == request.Id && x.Habitacion.SedeId == sede, ct) ?? throw new NotFoundException("Bloqueo", request.Id);
        if (!b.Activo) return;
        b.Activo = false; b.CanceladoPorUsuarioId = usuario; b.CanceladoUtc = DateTime.UtcNow;
        db.Auditoria.Add(new Auditoria { FechaUtc = DateTime.UtcNow, UsuarioId = usuario, SedeId = sede, Accion = "UPDATE", Entidad = "BloqueoHabitacion", EntidadId = b.Id.ToString(), Despues = "{\"operacion\":\"CancelarBloqueo\"}" });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
