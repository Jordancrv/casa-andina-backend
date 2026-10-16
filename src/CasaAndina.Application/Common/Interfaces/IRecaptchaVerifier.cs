namespace CasaAndina.Application.Common.Interfaces;

public interface IRecaptchaVerifier
{
    Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken);
}
