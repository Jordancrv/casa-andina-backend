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

public sealed class CancelarReservaCommandHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<CancelarReservaCommand>
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
            reserva.Habitaciones.Any(h => h.FechaCheckInReal != null || h.FechaCheckOutReal != null || h.FechaCheckIn.Date < PeruBusinessTime.Today))
            throw new ConflictException("Solo puedes cancelar reservas vigentes antes del check-in y hasta la fecha de entrada.");
        reserva.Estado = "Cancelada";
        db.Auditoria.Add(new Auditoria { FechaUtc = DateTime.UtcNow, UsuarioId = usuarioId, SedeId = reserva.SedeId,
            Accion = "UPDATE", Entidad = "Reserva", EntidadId = reserva.Id.ToString(),
            Antes = JsonSerializer.Serialize(new { Estado = "Reservado" }),
            Despues = JsonSerializer.Serialize(new { Estado = "Cancelada", Devolucion = "No procesada: pago simulado" }) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
