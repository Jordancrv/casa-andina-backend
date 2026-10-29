namespace CasaAndina.Domain.Entities;

public sealed class ClienteHuesped
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = default!;
    public int HuespedId { get; set; }
    public Huesped Huesped { get; set; } = default!;
    public string Nombres { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public string Dni { get; set; } = "";
    public DateTime FechaNacimiento { get; set; }
    public string Sexo { get; set; } = "";
    public string Correo { get; set; } = "";
    public string? Telefono { get; set; }
}
