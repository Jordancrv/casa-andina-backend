using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CasaAndina.Domain.Entities;

[Table("ValoracionViaje")]
public class ValoracionViaje
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public DateTime FechaUtc { get; set; }
    public bool Util { get; set; }
    [MaxLength(500)] public string? Motivo { get; set; }
    [MaxLength(100)] public string? Ciudad { get; set; }
    public DateTimeOffset PlanGeneradoEn { get; set; }
}
