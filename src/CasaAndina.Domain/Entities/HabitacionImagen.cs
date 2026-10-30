namespace CasaAndina.Domain.Entities;

public class HabitacionImagen
{
    public int Id { get; set; }
    public int HabitacionId { get; set; }
    public Habitacion Habitacion { get; set; } = default!;
    public string Url { get; set; } = string.Empty;
    public int Orden { get; set; }
}
