namespace CasaAndina.Domain.Entities;

public class Persona
{
    public int Id { get; set; }
    public string TipoPersona { get; set; } = "N";
    public string Estado { get; set; } = "Activo";
    public DateTime FechaRegistro { get; set; }
    public PersonaNatural? Natural { get; set; }
    public PersonaJuridica? Juridica { get; set; }
    public Usuario? Usuario { get; set; }
    public Cliente? Cliente { get; set; }
    public ICollection<PersonaDocumento> Documentos { get; set; } = new List<PersonaDocumento>();
    public ICollection<PersonaContacto> Contactos { get; set; } = new List<PersonaContacto>();
}

public class PersonaNatural
{
    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = default!;
    public string Nombres { get; set; } = default!;
    public string? Apellidos { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public string? Genero { get; set; }
    public string? Nacionalidad { get; set; }
    public Huesped? Huesped { get; set; }
}

public class PersonaJuridica
{
    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = default!;
    public string RazonSocial { get; set; } = default!;
    public string? NombreComercial { get; set; }
}

public class TipoDocumento
{
    public int Id { get; set; }
    public string Codigo { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public string TipoPersonaPermitida { get; set; } = default!;
    public byte LongitudMinima { get; set; }
    public byte LongitudMaxima { get; set; }
    public bool Activo { get; set; } = true;
    public ICollection<PersonaDocumento> Documentos { get; set; } = new List<PersonaDocumento>();
}

public class PersonaDocumento
{
    public int Id { get; set; }
    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = default!;
    public int TipoDocumentoId { get; set; }
    public TipoDocumento TipoDocumento { get; set; } = default!;
    public string Numero { get; set; } = default!;
    public string? PaisEmision { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public bool EsPrincipal { get; set; }
}

public class PersonaContacto
{
    public int Id { get; set; }
    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = default!;
    public string TipoContacto { get; set; } = default!;
    public string Valor { get; set; } = default!;
    public bool EsPrincipal { get; set; }
    public bool Verificado { get; set; }
}

public class Huesped
{
    public int Id { get; set; }
    public int PersonaId { get; set; }
    public PersonaNatural PersonaNatural { get; set; } = default!;
    public DateTime FechaRegistro { get; set; }
    public ICollection<AsignacionHuesped> Asignaciones { get; set; } = new List<AsignacionHuesped>();
}

public class AsignacionHuesped
{
    public int ReservaHabitacionId { get; set; }
    public ReservaHabitacion ReservaHabitacion { get; set; } = default!;
    public int HuespedId { get; set; }
    public Huesped Huesped { get; set; } = default!;
    public bool EsTitular { get; set; }
    public string? Observacion { get; set; }
}
