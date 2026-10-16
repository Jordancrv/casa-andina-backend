using CasaAndina.Application.Common.Exceptions;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Application.Reservas.DTOs;
using CasaAndina.Application.Reservas.Queries;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Data;
using ValidationException = CasaAndina.Application.Common.Exceptions.ValidationException;

namespace CasaAndina.Application.Reservas.Commands;

public sealed class CrearReservaCompraCommandValidator : AbstractValidator<CrearReservaCompraCommand>
{
    public CrearReservaCompraCommandValidator()
    {
        RuleFor(x => x.SedeId).GreaterThan(0);
        RuleFor(x => x.FechaCheckIn).Must((x, start) => start.Date >= DateTime.Today && x.FechaCheckOut.Date > start.Date)
            .WithMessage("El rango de estadía no es válido.");
        RuleFor(x => x.Habitaciones).Cascade(CascadeMode.Stop)
            .NotNull().NotEmpty().Must(x => x.Count <= 8)
            .WithMessage("Selecciona entre una y ocho habitaciones.");
        RuleFor(x => x.Habitaciones).Cascade(CascadeMode.Stop).NotNull()
            .Must(x => x.All(r => r is not null) && x.Select(r => r.HabitacionId).Distinct().Count() == x.Count)
            .WithMessage("No repitas una habitación en la reserva.");
        RuleForEach(x => x.Habitaciones).Where(room => room is not null).ChildRules(room =>
        {
            room.RuleFor(x => x.HabitacionId).GreaterThan(0);
            room.RuleFor(x => x.Huespedes).Cascade(CascadeMode.Stop)
                .NotNull().NotEmpty().Must(x => x.Count <= 10);
            room.RuleFor(x => x.Huespedes).Cascade(CascadeMode.Stop).NotNull()
                .Must(x => x.All(g => g is not null))
                .WithMessage("La lista de huéspedes contiene un valor inválido.")
                .Must(x => x.Count(g => g.EsTitular) == 1)
                .WithMessage("Cada habitación debe tener exactamente un huésped titular.");
            room.RuleForEach(x => x.Huespedes).Where(guest => guest is not null).ChildRules(guest =>
            {
                guest.RuleFor(x => x.Nombres).NotEmpty().MaximumLength(200);
                guest.RuleFor(x => x.Apellidos).NotEmpty().MaximumLength(100);
                guest.RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(150);
                guest.RuleFor(x => x.Telefono).MaximumLength(20);
            });
        });
        RuleFor(x => x.Servicios).Cascade(CascadeMode.Stop).NotNull()
            .Must(x => x.All(s => s is not null) && x.Select(s => s.ServicioId).Distinct().Count() == x.Count)
            .WithMessage("No repitas un servicio; ajusta su cantidad.");
        RuleForEach(x => x.Servicios).Where(service => service is not null).ChildRules(service =>
        {
            service.RuleFor(x => x.ServicioId).GreaterThan(0);
            service.RuleFor(x => x.Cantidad).InclusiveBetween(1, 20);
        });
        RuleFor(x => x.TipoComprobante).Must(x => Enum.TryParse<TipoComprobante>(x, true, out _))
            .WithMessage("El comprobante debe ser Boleta o Factura.");
        RuleFor(x => x.RazonSocial).Null().When(x => string.Equals(x.TipoComprobante, "Boleta", StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.Ruc).Null().When(x => string.Equals(x.TipoComprobante, "Boleta", StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.RazonSocial).NotEmpty().MaximumLength(200)
            .When(x => string.Equals(x.TipoComprobante, "Factura", StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.Ruc).NotEmpty().Matches("^\\d{11}$")
            .When(x => string.Equals(x.TipoComprobante, "Factura", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class CrearReservaCompraCommandHandler : IRequestHandler<CrearReservaCompraCommand, ReservaCompraResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CrearReservaCompraCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ReservaCompraResponseDto> Handle(CrearReservaCompraCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var clienteId = _currentUser.ClienteId
            ?? throw new UnauthorizedAccessException("El token no identifica a un cliente.");
        var cliente = await _context.Clientes.Include(x => x.Persona)
            .Include(x => x.Persona).ThenInclude(x => x.Juridica)
            .SingleOrDefaultAsync(x => x.Id == clienteId && x.Estado == "Activo", cancellationToken)
            ?? throw new ForbiddenAccessException("No se encontró un perfil de cliente activo.");
        var checkIn = request.FechaCheckIn.Date;
        var checkOut = request.FechaCheckOut.Date;
        GetDisponibilidadQueryHandler.ValidarFechas(request.SedeId, checkIn, checkOut);
        var nights = (checkOut - checkIn).Days;
        if (nights > 90) throw Invalid("La estadía no puede superar 90 noches.");

        var roomsRequested = request.Habitaciones.Select(x => x.HabitacionId).Distinct().ToArray();
        if (roomsRequested.Length != request.Habitaciones.Count) throw Invalid("No repitas una habitación en la reserva.");
        var rooms = await _context.Habitaciones.Include(x => x.TipoHabitacion)
            .Where(x => roomsRequested.Contains(x.Id) && x.SedeId == request.SedeId && x.Activo &&
                (x.Estado == "Disponible" || x.Estado == "Ocupada"))
            .ToListAsync(cancellationToken);
        if (rooms.Count != roomsRequested.Length) throw new ConflictException("Una o más habitaciones ya no están disponibles en esta sede.");
        var alreadyReserved = await _context.ReservaHabitaciones.AnyAsync(rh =>
            roomsRequested.Contains(rh.HabitacionId) && rh.FechaCheckIn < checkOut && rh.FechaCheckOut > checkIn &&
            rh.Reserva.Estado != "Cancelada", cancellationToken);
        if (alreadyReserved) throw new ConflictException("La disponibilidad cambió. Vuelve a buscar habitaciones antes de confirmar.");

        var guestEmailSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var allocation in request.Habitaciones)
        {
            var room = rooms.Single(x => x.Id == allocation.HabitacionId);
            var capacity = room.TipoHabitacion.CapacidadAdultos + room.TipoHabitacion.CapacidadNinos;
            if (allocation.Huespedes.Count > capacity)
                throw Invalid($"La habitación {room.Numero} admite como máximo {capacity} huéspedes.");
            var roomSubtotal = decimal.Round(room.PrecioNoche * nights, 2);
            ReservaCompraRules.ValidarImportePersistible(roomSubtotal, "Subtotal de habitación");
            foreach (var guest in allocation.Huespedes)
            {
                if (!guestEmailSet.Add(guest.Correo.Trim().ToLowerInvariant()))
                    throw Invalid("Cada huésped debe tener un correo único dentro de la reserva.");
            }
        }

        var servicesRequested = request.Servicios.Select(x => x.ServicioId).Distinct().ToArray();
        var services = servicesRequested.Length == 0 ? new List<Servicio>() : await _context.Servicios
            .Include(x => x.Sedes)
            .Where(x => servicesRequested.Contains(x.Id) && x.Activo && x.Sedes.Any(s => s.Id == request.SedeId))
            .ToListAsync(cancellationToken);
        if (services.Count != servicesRequested.Length) throw Invalid("Uno o más servicios no están activos en la sede seleccionada.");

        var tipoComprobante = Enum.Parse<TipoComprobante>(request.TipoComprobante, true);
        if (tipoComprobante == TipoComprobante.Factura)
        {
            var registeredRuc = await _context.PersonaDocumentos
                .Where(d => d.PersonaId == cliente.PersonaId && d.TipoDocumento.Codigo == "RUC")
                .Select(d => d.Numero).SingleOrDefaultAsync(cancellationToken);
            if (!string.Equals(cliente.Persona.Juridica?.RazonSocial, request.RazonSocial?.Trim(), StringComparison.OrdinalIgnoreCase))
                throw Invalid("La factura debe emitirse a la razón social registrada en el perfil del cliente.");
            if (registeredRuc is null || !string.Equals(registeredRuc, request.Ruc, StringComparison.Ordinal))
                throw Invalid("El RUC debe estar registrado en el perfil jurídico del cliente.");
        }

        var reservation = new Reserva
        {
            CodigoReserva = $"CA-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            ClienteId = clienteId,
            SedeId = request.SedeId,
            CreadoPorUsuarioId = _currentUser.UsuarioId,
            Estado = "Confirmada",
            Canal = "Directo",
            PrecioTotal = 0,
            FechaCreacion = DateTime.UtcNow
        };

        foreach (var allocation in request.Habitaciones)
        {
            var room = rooms.Single(x => x.Id == allocation.HabitacionId);
            var roomSubtotal = decimal.Round(room.PrecioNoche * nights, 2);
            var detail = new ReservaHabitacion
            {
                HabitacionId = room.Id,
                FechaCheckIn = checkIn,
                FechaCheckOut = checkOut,
                CantidadPersonas = allocation.Huespedes.Count,
                PrecioAcordado = room.PrecioNoche,
                Subtotal = roomSubtotal
            };
            reservation.PrecioTotal = ReservaCompraRules.ValidarImportePersistible(
                reservation.PrecioTotal + detail.Subtotal, "Total de reserva");
            foreach (var guest in allocation.Huespedes)
            {
                var guestId = await FindOrCreateGuest(guest, cancellationToken);
                detail.Huespedes.Add(new AsignacionHuesped
                {
                    HuespedId = guestId,
                    EsTitular = guest.EsTitular
                });
            }
            reservation.Habitaciones.Add(detail);
        }

        foreach (var requestedService in request.Servicios)
        {
            var service = services.Single(x => x.Id == requestedService.ServicioId);
            var serviceSubtotal = service.Precio * requestedService.Cantidad;
            ReservaCompraRules.ValidarImportePersistible(serviceSubtotal, "Subtotal de servicio");
            reservation.Servicios.Add(new ReservaServicio
            {
                ServicioId = service.Id,
                Cantidad = requestedService.Cantidad,
                PrecioUnitario = service.Precio
            });
            reservation.PrecioTotal = ReservaCompraRules.ValidarImportePersistible(
                reservation.PrecioTotal + serviceSubtotal, "Total de reserva");
        }

        var bookingNumber = $"B{DateTime.UtcNow:yyyyMMdd}{reservation.CodigoReserva[^5..]}";
        reservation.Comprobante = new Comprobante
        {
            NumeroComprobante = bookingNumber,
            Tipo = tipoComprobante,
            FechaEmision = DateTime.UtcNow,
            MontoTotal = reservation.PrecioTotal
        };
        _context.Reservas.Add(reservation);

        _context.Auditoria.Add(new Auditoria
        {
            FechaUtc = DateTime.UtcNow,
            UsuarioId = _currentUser.UsuarioId,
            SedeId = request.SedeId,
            Accion = "INSERT",
            Entidad = "Reserva",
            EntidadId = reservation.CodigoReserva,
            Despues = System.Text.Json.JsonSerializer.Serialize(new
            {
                reservation.Estado,
                reservation.PrecioTotal,
                Habitaciones = reservation.Habitaciones.Count,
                Servicios = reservation.Servicios.Count,
                TipoComprobante = tipoComprobante.ToString(),
                Pago = "Simulado"
            })
        });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ReservaCompraResponseDto(reservation.Id, reservation.CodigoReserva,
            reservation.Estado, reservation.PrecioTotal, tipoComprobante.ToString(),
            "Compra simulada correctamente. No se realizó ningún cobro real.");
    }

    private async Task<int> FindOrCreateGuest(HuespedCompraDto guest, CancellationToken cancellationToken)
    {
        var email = guest.Correo.Trim().ToLowerInvariant();
        var existingContact = await _context.PersonaContactos.Include(x => x.Persona)
            .ThenInclude(x => x.Natural)
            .SingleOrDefaultAsync(x => x.TipoContacto == "Correo" && x.Valor.ToLower() == email, cancellationToken);
        if (existingContact is not null)
        {
            var existingGuest = await _context.Huespedes.SingleOrDefaultAsync(x => x.PersonaId == existingContact.PersonaId, cancellationToken);
            if (existingGuest is not null) return existingGuest.Id;
            if (existingContact.Persona.Natural is null)
                throw new ConflictException("El correo indicado pertenece a una persona jurídica y no puede registrarse como huésped.");
            var linkedGuest = new Huesped { PersonaId = existingContact.PersonaId, FechaRegistro = DateTime.UtcNow };
            _context.Huespedes.Add(linkedGuest);
            await _context.SaveChangesAsync(cancellationToken);
            return linkedGuest.Id;
        }

        var persona = new Persona
        {
            TipoPersona = "N",
            Estado = "Activo",
            FechaRegistro = DateTime.UtcNow,
            Natural = new PersonaNatural { Nombres = guest.Nombres.Trim(), Apellidos = guest.Apellidos.Trim() },
            Contactos =
            {
                new PersonaContacto { TipoContacto = "Correo", Valor = email, EsPrincipal = true },
            }
        };
        if (!string.IsNullOrWhiteSpace(guest.Telefono))
            persona.Contactos.Add(new PersonaContacto { TipoContacto = "Telefono", Valor = guest.Telefono.Trim(), EsPrincipal = true });
        var newGuest = new Huesped { PersonaNatural = persona.Natural, FechaRegistro = DateTime.UtcNow };
        persona.Natural.Huesped = newGuest;
        _context.Personas.Add(persona);
        await _context.SaveChangesAsync(cancellationToken);
        return newGuest.Id;
    }

    private static ValidationException Invalid(string message)
        => new([new ValidationFailure("Reserva", message)]);
}
