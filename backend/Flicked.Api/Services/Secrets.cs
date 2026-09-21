using System.Security.Cryptography;
using System.Text;

namespace Flicked.Api.Services;

// Making and hashing the random strings used as session tokens and login codes.
public static class Secrets
{
    public static string New() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
