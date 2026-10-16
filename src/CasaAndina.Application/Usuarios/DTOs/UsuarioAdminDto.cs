namespace CasaAndina.Application.Usuarios.DTOs;

public sealed record UsuarioAdminDto(
    int UsuarioId,
    string Nombre,
    string? Correo,
    string? Telefono,
    string TipoPersona,
    int? SedeId,
    string? SedeNombre,
    DateTime FechaRegistro,
    IReadOnlyList<string> Roles,
    bool Activo);

public sealed record ActualizarCuentaUsuarioCommand(int UsuarioId, string Correo, string? Telefono)
    : MediatR.IRequest;
