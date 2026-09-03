using CasaAndina.Domain.Entities;

namespace CasaAndina.Application.Common.Interfaces;

public interface IJwtService
{
    string GenerarToken(Usuario usuario);
}
