namespace CasaAndina.Domain.Entities;

/// <summary>RF07: filtro activa/histórica, comprobante digital.</summary>
public enum EstadoReserva
{
    Pendiente  = 1,
    Confirmada = 2,
    Bloqueada  = 3,
    Completada = 4,
    Cancelada  = 5
}
