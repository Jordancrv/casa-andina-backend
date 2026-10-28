namespace CasaAndina.Domain.Entities;

public sealed class ConexionCalendario
{
    public int ClienteId { get; set; }
    public string? CalendarioId { get; set; }
    public string? RefreshTokenProtegido { get; set; }
    public bool RequiereReconectar { get; set; }
    public DateTime? UltimaSincronizacionUtc { get; set; }
}

public sealed class EventoCalendario
{
    public int ClienteId { get; set; }
    public int ReservaId { get; set; }
    public string EventoId { get; set; } = Guid.NewGuid().ToString("N");
    public string? Huella { get; set; }
}
