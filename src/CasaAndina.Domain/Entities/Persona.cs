namespace CasaAndina.Domain.Entities;

public class Persona
{
    public int Id { get; set; }
    public string TipoPersona { get; set; } = "N";
    public string Estado { get; set; } = "Activo";
    public DateTime FechaRegistro { get; set; }
    public PersonaNatural? Natural { get; set; }
    public PersonaJuridica? Juridica { get; set; }
    public Cliente? Cliente { get; set; }
    public ICollection<PersonaDocumento> Documentos { get; set; } = new List<PersonaDocumento>();
    public ICollection<PersonaCorreo> Correos { get; set; } = new List<PersonaCorreo>();
    public ICollection<PersonaTelefono> Telefonos { get; set; } = new List<PersonaTelefono>();
}

public class PersonaNatural
{
    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = default!;
    public string Nombres { get; set; } = default!;
    public string? Apellidos { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public string? Nacionalidad { get; set; }
    public ICollection<ReservaHuesped> Reservas { get; set; } = new List<ReservaHuesped>();
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

public class PersonaCorreo
{
    public int Id { get; set; }
    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = default!;
    public string Correo { get; set; } = default!;
    public bool EsPrincipal { get; set; }
    public bool Verificado { get; set; }
}

public class PersonaTelefono
{
    public int Id { get; set; }
    public int PersonaId { get; set; }
    public Persona Persona { get; set; } = default!;
    public string Numero { get; set; } = default!;
    public bool EsPrincipal { get; set; }
}

public class ReservaHuesped
{
    public int ReservaId { get; set; }
    public Reserva Reserva { get; set; } = default!;
    public int PersonaId { get; set; }
    public PersonaNatural Huesped { get; set; } = default!;
    public bool EsTitular { get; set; }
    public string? Observacion { get; set; }
}
