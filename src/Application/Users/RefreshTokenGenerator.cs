using System.Security.Cryptography;

namespace Application.Users;

internal static class RefreshTokenGenerator
{
    /// <summary>Url-safe opaque token (64 random bytes).</summary>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
