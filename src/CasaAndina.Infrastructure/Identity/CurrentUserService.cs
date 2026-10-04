using System.Security.Claims;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace CasaAndina.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public int? UsuarioId
        => GetIdentityId();

    public int? ClienteId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(AuthClaimTypes.ClienteId);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public int? SedeId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User
                .FindFirstValue(AuthClaimTypes.SedeId);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public RolUsuario? Rol
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<RolUsuario>(value, out var rol) ? rol : null;
        }
    }

    private int? GetIdentityId()
    {
        var value = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier)
            ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

        return int.TryParse(value, out var id) ? id : null;
    }
}
