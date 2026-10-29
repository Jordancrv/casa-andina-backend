namespace CasaAndina.Domain.Entities;

public sealed class ReporteAnalisis
{
    public long Id { get; set; }
    public int SedeId { get; set; }
    public int UsuarioId { get; set; }
    public string Periodo { get; set; } = string.Empty;
    public DateTime GeneradoUtc { get; set; }
    public string ContenidoJson { get; set; } = string.Empty;
}
