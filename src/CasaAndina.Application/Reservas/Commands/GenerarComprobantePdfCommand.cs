using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.DTOs;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CasaAndina.Application.Reservas.Commands;

public sealed record GenerarComprobantePdfCommand(int ReservaId) : IRequest<ComprobantePdfDto>;
public sealed class GenerarComprobantePdfCommandValidator : AbstractValidator<GenerarComprobantePdfCommand>
{
    public GenerarComprobantePdfCommandValidator() => RuleFor(q => q.ReservaId).GreaterThan(0);
}

public sealed class GenerarComprobantePdfCommandHandler(IApplicationDbContext context, ICurrentUserService user, IComprobantePdfService pdf)
    : IRequestHandler<GenerarComprobantePdfCommand, ComprobantePdfDto>
{
    public async Task<ComprobantePdfDto> Handle(GenerarComprobantePdfCommand request, CancellationToken ct)
    {
        var clienteId = user.ClienteId ?? throw new UnauthorizedAccessException("El token no identifica a un cliente.");
        ComprobanteDocumentoDto documento;
        await using (var transaction = await context.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var reserva = await context.Reservas.Include(r => r.Comprobante)
                .SingleOrDefaultAsync(r => r.Id == request.ReservaId && r.ClienteId == clienteId, ct)
                ?? throw new NotFoundException("Reserva", request.ReservaId);
            if (reserva.Comprobante is null) throw new ConflictException("Esta reserva no tiene un comprobante registrado.");
            var montoAprobado = await context.DetallesPago.Where(d => d.ReservaId == reserva.Id && d.Pago.EstadoPago.Codigo == "APROBADO" && d.Pago.Moneda == "PEN")
                .Select(d => (decimal?)d.MontoAplicado).SumAsync(ct) ?? 0;
            if (reserva.Comprobante.MontoTotal <= 0 || montoAprobado < reserva.Comprobante.MontoTotal)
                throw new ConflictException("El comprobante estará disponible cuando los pagos aprobados cubran el total de la reserva.");
            if (reserva.Comprobante.DatosEmisionJson is null)
            {
                // Compatibilidad con reservas anteriores: se informa que el detalle se reconstruyó.
                documento = await ComprobanteEmision.CrearAsync(context, reserva, reconstruido: true, ct);
                reserva.Comprobante.DatosEmisionJson = JsonSerializer.Serialize(documento, ComprobanteEmision.JsonOptions);
                await context.SaveChangesAsync(ct);
            }
            else documento = ComprobanteEmision.Leer(reserva);
            await transaction.CommitAsync(ct);
        }
        ct.ThrowIfCancellationRequested();
        var numeroSeguro = Regex.Replace(documento.Numero, "[^a-zA-Z0-9_-]", "_");
        return new(pdf.Generar(documento), $"{documento.Tipo}-{numeroSeguro}.pdf");
    }
}
