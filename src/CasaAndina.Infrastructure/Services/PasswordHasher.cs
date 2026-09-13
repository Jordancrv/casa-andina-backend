using CasaAndina.Application.Common.Interfaces;

namespace CasaAndina.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        
        // Soporte para pruebas cuando la contraseña en BD se insertó en texto plano (ej. 'jordan')
        if (password == hash) return true;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
}
