namespace CasaAndina.Domain.Entities;

public sealed class Auditoria
{
    public long Id { get; set; }
    public DateTime FechaUtc { get; set; }
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public int? SedeId { get; set; }
    public Sede? Sede { get; set; }
    public string Accion { get; set; } = default!;
    public string Entidad { get; set; } = default!;
    public string EntidadId { get; set; } = default!;
    public string? Antes { get; set; }
    public string? Despues { get; set; }
    public string? DireccionIp { get; set; }
    public Guid? CorrelationId { get; set; }
}
