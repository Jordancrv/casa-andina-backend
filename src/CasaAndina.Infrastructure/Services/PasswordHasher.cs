using CasaAndina.Application.Common.Interfaces;

namespace CasaAndina.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private static readonly string DummyHash;

    static PasswordHasher()
        => DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash)) return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }

    public void VerifyAgainstDummyHash(string password)
        => Verify(password, DummyHash);
}
