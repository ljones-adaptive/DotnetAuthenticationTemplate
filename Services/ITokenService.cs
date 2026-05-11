namespace ScalpingApp.Services;

public interface ITokenService
{
    string Generate(string data, string salt);
    string? Verify(string token, string salt, int maxAgeSeconds = 86400);
}
