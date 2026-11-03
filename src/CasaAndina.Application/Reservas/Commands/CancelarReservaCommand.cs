using System.Data;
using System.Text.Json;
using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CasaAndina.Application.Reservas.Commands;

public sealed record CancelarReservaCommand(int Id) : IRequest;
public sealed class CancelarReservaCommandValidator : AbstractValidator<CancelarReservaCommand>
{
    public CancelarReservaCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public sealed class CancelarReservaCommandHandler(IApplicationDbContext db, ICurrentUserService user, TimeProvider? clock = null) : IRequestHandler<CancelarReservaCommand>
{
    public async Task Handle(CancelarReservaCommand request, CancellationToken ct)
    {
        var clienteId = user.ClienteId ?? throw new UnauthorizedAccessException("El token no identifica a un cliente.");
        if (user.UsuarioId is not int usuarioId) throw new UnauthorizedAccessException("El token no identifica una cuenta.");
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var reserva = await db.Reservas.Include(r => r.Habitaciones)
            .SingleOrDefaultAsync(r => r.Id == request.Id && r.ClienteId == clienteId, ct)
            ?? throw new NotFoundException("Reserva", request.Id);
        if (reserva.Estado == "Cancelada") return;
        if (reserva.Estado != "Reservado" || reserva.Habitaciones.Count == 0 ||
            reserva.Habitaciones.Any(h => h.FechaCheckInReal != null || h.FechaCheckOutReal != null))
            throw new ConflictException("Solo puedes cancelar reservas vigentes cuya estadía no haya comenzado.");
        // La primera entrada determina el plazo de toda la compra, incluso con varias habitaciones.
        var limiteUtc = PeruBusinessTime.UtcStartOfDay(reserva.Habitaciones.Min(h => h.FechaCheckIn).Date.AddDays(-2)).AddHours(10);
        var ahoraUtc = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (ahoraUtc > limiteUtc)
            throw new ConflictException("El plazo de cancelación venció. Solo puedes cancelar hasta las 10:00 a. m. (hora de Perú), dos días antes de la fecha de entrada.");
        reserva.Estado = "Cancelada";
        db.Auditoria.Add(new Auditoria { FechaUtc = ahoraUtc, UsuarioId = usuarioId, SedeId = reserva.SedeId,
            Accion = "UPDATE", Entidad = "Reserva", EntidadId = reserva.Id.ToString(),
            Antes = JsonSerializer.Serialize(new { Estado = "Reservado" }),
            Despues = JsonSerializer.Serialize(new { Estado = "Cancelada", Devolucion = "No procesada: pago simulado" }) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
