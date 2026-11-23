namespace CasaAndina.Domain.Entities;

public sealed class BloqueoHabitacion
{
    public int Id { get; set; }
    public int HabitacionId { get; set; }
    public Habitacion Habitacion { get; set; } = default!;
    public DateTime Inicio { get; set; }
    public DateTime FinExclusivo { get; set; }
    public string Motivo { get; set; } = default!;
    public bool Activo { get; set; } = true;
    public int CreadoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; }
    public int? CanceladoPorUsuarioId { get; set; }
    public DateTime? CanceladoUtc { get; set; }
}
