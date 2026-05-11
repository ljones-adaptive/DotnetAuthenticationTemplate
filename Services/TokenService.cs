using System.Security.Cryptography;
using System.Text;

namespace ScalpingApp.Services;

public class TokenService : ITokenService
{
    private readonly byte[] _key;

    public TokenService(IConfiguration config)
    {
        var secret = config["AppSettings:SecretKey"] ?? "dev-secret-change-this";
        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string Generate(string data, string salt)
    {
        var payload = $"{data}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{salt}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(_key);
        var sig = hmac.ComputeHash(payloadBytes);
        return $"{Convert.ToBase64String(payloadBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')}" +
               $".{Convert.ToBase64String(sig).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
    }

    public string? Verify(string token, string salt, int maxAgeSeconds = 86400)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 2) return null;

            var payloadBytes = Convert.FromBase64String(Pad(parts[0]).Replace('-', '+').Replace('_', '/'));
            var sig = Convert.FromBase64String(Pad(parts[1]).Replace('-', '+').Replace('_', '/'));

            using var hmac = new HMACSHA256(_key);
            var expected = hmac.ComputeHash(payloadBytes);
            if (!CryptographicOperations.FixedTimeEquals(expected, sig)) return null;

            var payload = Encoding.UTF8.GetString(payloadBytes);
            var segments = payload.Split('|');
            if (segments.Length != 3) return null;

            var timestamp = long.Parse(segments[1]);
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp > maxAgeSeconds) return null;
            if (segments[2] != salt) return null;

            return segments[0];
        }
        catch { return null; }
    }

    private static string Pad(string s)
    {
        int rem = s.Length % 4;
        if (rem == 2) return s + "==";
        if (rem == 3) return s + "=";
        return s;
    }
}
