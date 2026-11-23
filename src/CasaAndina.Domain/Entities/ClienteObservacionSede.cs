namespace CasaAndina.Domain.Entities;

public sealed class ClienteObservacionSede
{
    public int ClienteId { get; set; }
    public int SedeId { get; set; }
    public string Observacion { get; set; } = string.Empty;
    public int? ActualizadoPorUsuarioId { get; set; }
    public DateTime ActualizadoUtc { get; set; }
}
