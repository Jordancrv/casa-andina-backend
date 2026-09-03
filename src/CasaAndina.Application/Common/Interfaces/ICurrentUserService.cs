using CasaAndina.Domain.Enums;

namespace CasaAndina.Application.Common.Interfaces;

/// <summary>Lee el usuario autenticado a partir del JWT de la request actual (RNF01).</summary>
public interface ICurrentUserService
{
    int? UsuarioId { get; }
    RolUsuario? Rol { get; }
}
